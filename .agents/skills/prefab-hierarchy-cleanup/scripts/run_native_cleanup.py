"""NativeUnity cleanup runner. Logs real boundaries; never replays an apply."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from uuid import uuid4


class RunLog:
    def __init__(self, directory, target):
        self.directory = Path(directory).resolve()
        self.directory.mkdir(parents=True, exist_ok=True)
        self.path = self.directory / 'events.jsonl'
        self.run_id = uuid4().hex
        self.target = target
        self.started = time.perf_counter()
        self.steps = []

    def emit(self, step, event, **fields):
        item = dict(run_id=self.run_id, target=self.target, step=step, event=event,
                    utc=datetime.now(timezone.utc).isoformat(),
                    run_elapsed_ms=round((time.perf_counter() - self.started) * 1000, 3), **fields)
        with self.path.open('a', encoding='utf-8') as stream:
            stream.write(json.dumps(item, ensure_ascii=False) + '\n')
            stream.flush()
        print(json.dumps(item, ensure_ascii=True), flush=True)
        if event == 'end':
            self.steps.append(item)

    def finish(self, status):
        summary = dict(run_id=self.run_id, target=self.target, status=status,
                       elapsed_ms=round((time.perf_counter() - self.started) * 1000, 3),
                       steps=self.steps, slowest_steps=sorted(self.steps, key=lambda x: x['elapsed_ms'], reverse=True))
        (self.directory / 'summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')


def run_process(log, step, command, cwd, timeout=50, heartbeat=5, validator=None):
    started = time.perf_counter()
    log.emit(step, 'start', executable=str(command[0]))
    process = None
    status = 'failed'
    error = None
    try:
        env = dict(os.environ, PYTHONIOENCODING='utf-8')
        process = subprocess.Popen(command, cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   encoding='utf-8', errors='replace', env=env)
        while True:
            remaining = timeout - (time.perf_counter() - started)
            if remaining <= 0:
                raise subprocess.TimeoutExpired(command, timeout)
            try:
                stdout, stderr = process.communicate(timeout=min(heartbeat, remaining))
                break
            except subprocess.TimeoutExpired:
                log.emit(step, 'heartbeat', elapsed_ms=round((time.perf_counter() - started)*1000, 3),
                         status='waiting_for_process', pid=process.pid)
        (log.directory / (step + '.stdout.txt')).write_text(stdout, encoding='utf-8')
        (log.directory / (step + '.stderr.txt')).write_text(stderr, encoding='utf-8')
        if process.returncode != 0:
            raise RuntimeError(f'{step}: exit={process.returncode}; see {step}.stdout.txt / stderr.txt')
        result = validator(stdout) if validator else stdout
        status = 'passed'
        return result
    except subprocess.TimeoutExpired:
        status = 'timeout'
        if process is not None:
            # Stop only the CLI child, never Unity Editor. The editor operation
            # may still be running; a mutating timeout is an indeterminate save.
            process.kill()
            stdout, stderr = process.communicate()
            (log.directory / (step + '.stdout.txt')).write_text(stdout, encoding='utf-8')
            (log.directory / (step + '.stderr.txt')).write_text(stderr, encoding='utf-8')
        error = 'CLI timed out; Unity state may be indeterminate. Do not replay Apply.'
        raise TimeoutError(error)
    except BaseException as exc:
        error = str(exc)
        raise
    finally:
        log.emit(step, 'end', status=status, elapsed_ms=round((time.perf_counter()-started)*1000, 3),
                 exit_code=None if process is None else process.returncode, error=error)


def check_envelope(raw, mode):
    value = json.loads(raw)
    if not isinstance(value, dict) or value.get('success') is not True:
        raise ValueError('NativeUnity outer envelope failed: ' + raw[:2000])
    data = value.get('data')
    if not isinstance(data, dict) or data.get('success') is False:
        raise ValueError('NativeUnity command envelope failed')
    result = data.get('result')
    if not isinstance(result, dict) or result.get('success') is not True:
        raise ValueError('NativeUnity eval failed: ' + json.dumps(result, ensure_ascii=False)[:2000])
    payload = result.get('result')
    if not isinstance(payload, str):
        raise ValueError('NativeUnity returned no string result')
    if 'VERIFY_WARN' in payload:
        raise ValueError(payload)
    marker = {'preflight': 'PREFLIGHT_OK', 'apply': 'VERIFY_OK ', 'verify': 'VERIFY_OK ', 'snapshot': 'SNAPSHOT_BEGIN'}[mode]
    if not payload.startswith(marker):
        raise ValueError(f'{mode}: missing {marker} result: {payload[:1000]}')
    if mode == 'snapshot' and 'SNAPSHOT_END' not in payload:
        raise ValueError('Incomplete snapshot')
    return payload


def claim_apply(project, target, plan_bytes):
    directory = Path(project) / 'Library' / 'PrefabCleanupApplyLedger'
    directory.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(target.encode('utf-8') + b'\0' + plan_bytes).hexdigest()
    marker = directory / (digest + '.json')
    # Exclusive creation persists across run folders and failures. No auto reset.
    with marker.open('x', encoding='utf-8') as stream:
        json.dump(dict(target=target, plan_sha256=hashlib.sha256(plan_bytes).hexdigest(),
                       utc=datetime.now(timezone.utc).isoformat(), status='apply_attempted'), stream)
    return marker


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-path', required=True, type=Path)
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--prefab-path')
    parser.add_argument('--mode', choices=['snapshot', 'preflight', 'apply', 'verify'], required=True)
    parser.add_argument('--apply-confirmed', action='store_true')
    parser.add_argument('--timeout', type=float, default=50)
    parser.add_argument('--heartbeat', type=float, default=5)
    args = parser.parse_args()
    project = args.project_path.resolve()
    if not (project / 'Assets').is_dir() or not (project / 'ProjectSettings').is_dir():
        parser.error('project-path must be an existing Unity project')
    if args.timeout <= 0 or args.heartbeat <= 0 or args.heartbeat > 30:
        parser.error('timeout must be positive; heartbeat must be >0 and <=30 seconds')
    if args.mode == 'apply' and not args.apply_confirmed:
        parser.error('Apply requires the already-reviewed plan and --apply-confirmed')
    if args.mode == 'snapshot':
        target = args.prefab_path
        plan_bytes = None
    else:
        if args.plan is None:
            parser.error('--plan is required')
        plan_bytes = args.plan.read_bytes()
        target = json.loads(plan_bytes)['prefabAssetPath']
    if not target or not target.startswith('Assets/') or not target.endswith('.prefab') or '..' in Path(target).parts:
        parser.error('Target must be a project-relative Assets/*.prefab')
    directory = project / 'Library' / 'PrefabCleanupRuns' / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S') + '-' + uuid4().hex[:8])
    log = RunLog(directory, target)
    status = 'failed'
    try:
        unity = shutil.which('unity')
        if not unity:
            raise RuntimeError('NativeUnity CLI not installed; no fallback or tool download permitted')
        renderer = Path(__file__).with_name('render_prefab_cleanup.py')
        frozen_plan = directory / 'plan.json'
        if plan_bytes is not None:
            frozen_plan.write_bytes(plan_bytes)
        phases = ['preflight', 'apply', 'verify'] if args.mode == 'apply' else [args.mode]
        for phase in phases:
            payload = directory / (phase + '.cs')
            command = [sys.executable, str(renderer), '--mode', phase, '--output', str(payload)]
            command += ['--prefab-path', target] if phase == 'snapshot' else ['--plan', str(frozen_plan)]
            run_process(log, phase + '.render', command, project, args.timeout, args.heartbeat)
            if phase == 'apply':
                claim_apply(project, target, plan_bytes)
            command = [unity, '--json', '--non-interactive', 'command', '--project-path', str(project),
                       '--timeout', str(max(1, int(args.timeout)-5)), 'eval_file', '--', '--file', str(payload),
                       '--timeout', str(max(1000, int((args.timeout-10)*1000)))]
            result = run_process(log, phase + '.unity', command, project, args.timeout, args.heartbeat,
                                 validator=lambda raw: check_envelope(raw, phase))
            (directory / (phase + '.result.txt')).write_text(result, encoding='utf-8')
        status = 'passed'
        return 0
    except Exception as exc:
        log.emit('workflow', 'error', error=str(exc), status='failed')
        return 1
    finally:
        log.finish(status)
        print('RUN_DIRECTORY=' + str(directory), flush=True)


if __name__ == '__main__':
    raise SystemExit(main())
