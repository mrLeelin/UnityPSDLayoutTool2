using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UGF.EditorTools.Psd2UGUI;
using AiCliProviderBaseNamespace;

namespace ClaudeCodeCliProviderNamespace
{
    internal sealed class ClaudeCodeCliProvider : AiCliProviderBase
    {
        internal static ClaudeCodeCliProvider s_ObfuscationSentinel;

        [SpecialName]
        public override string GetProviderId()
        {
            return "claude-code-cli";
        }

        [SpecialName]
        public override AiProviderCapabilities GetCapabilities()
        {
            return new AiProviderCapabilities
            {
                SupportsImages = true,
                SupportsStrictJson = true,
                UsesVisibleCliExecution = true
            };
        }

        [SpecialName]
        protected override string GetExecutableName()
        {
            return "claude";
        }

        protected override string BuildCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "-p", "--output-format", "text", "--permission-mode", "bypassPermissions", "--no-session-persistence" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("--add-dir");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "--print", "--output-format", "stream-json", "--include-partial-messages", "--verbose", "--permission-mode", "bypassPermissions" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("--add-dir");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleRunnerInvocation(string commandArguments, AiJobContext jobContext)
        {
            return BuildVisibleCliAdapterScript(commandArguments, "\nfunction Convert-ClaudeVisibleCliEvent {\n    param([string]$Line)\n    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }\n    try { $ev = $Line | ConvertFrom-Json -ErrorAction Stop } catch { return [pscustomobject]@{ text = $Line; color = 'DarkGray' } }\n    switch ($ev.type) {\n        'system' {\n            if ($ev.subtype -eq 'init') {\n                return [pscustomobject]@{\n                    text = '==> Claude session started (model: ' + $ev.model + ', tools: ' + $ev.tools.Count + ')'\n                    color = 'Cyan'\n                    blankLineBefore = $true\n                    dedupeKey = 'session'\n                    dedupeValue = 'claude-init'\n                }\n            }\n        }\n        'stream_event' {\n            $e = $ev.event\n            if ($e.type -eq 'content_block_start') {\n                $script:currentContentBlockType = [string]$e.content_block.type\n                if ($e.content_block.type -eq 'thinking') {\n                    return [pscustomobject]@{\n                        text = '--- Thinking ---'\n                        color = 'DarkYellow'\n                        blankLineBefore = $true\n                        dedupeKey = 'section'\n                        dedupeValue = 'thinking'\n                    }\n                } elseif ($e.content_block.type -eq 'tool_use') {\n                    return [pscustomobject]@{\n                        text = '[Tool: ' + $e.content_block.name + ']'\n                        color = 'Green'\n                        blankLineBefore = $true\n                    }\n                } elseif ($e.content_block.type -eq 'text') {\n                    $script:currentTextBlockBuilder = New-Object System.Text.StringBuilder\n                    return [pscustomobject]@{\n                        text = '--- Response ---'\n                        color = 'White'\n                        blankLineBefore = $true\n                        dedupeKey = 'section'\n                        dedupeValue = 'response'\n                    }\n                }\n            } elseif ($e.type -eq 'content_block_delta') {\n                switch ($e.delta.type) {\n                    'thinking_delta'   {\n                        return [pscustomobject]@{\n                            text = [string]$e.delta.thinking\n                            color = 'DarkYellow'\n                            noNewline = $true\n                        }\n                    }\n                    'text_delta'       {\n                        if ($script:currentContentBlockType -eq 'text' -and $null -ne $e.delta.text) {\n                            [void]$script:allTextBuilder.Append([string]$e.delta.text)\n                            if ($script:currentTextBlockBuilder -ne $null) {\n                                [void]$script:currentTextBlockBuilder.Append([string]$e.delta.text)\n                            }\n                        }\n                        return [pscustomobject]@{\n                            text = [string]$e.delta.text\n                            color = 'White'\n                            noNewline = $true\n                        }\n                    }\n                    'input_json_delta' {\n                        return [pscustomobject]@{\n                            text = [string]$e.delta.partial_json\n                            color = 'DarkGreen'\n                            noNewline = $true\n                        }\n                    }\n                }\n            } elseif ($e.type -eq 'content_block_stop') {\n                if ($script:currentContentBlockType -eq 'text' -and $script:currentTextBlockBuilder -ne $null -and $script:currentTextBlockBuilder.Length -gt 0) {\n                    $script:lastTextBlockBuilder = $script:currentTextBlockBuilder\n                }\n                $script:currentTextBlockBuilder = $null\n                $script:currentContentBlockType = ''\n                return [pscustomobject]@{ newline = $true }\n            }\n        }\n        'user' {\n            $c = $ev.message.content[0]\n            if ($c.type -eq 'tool_result') {\n                $content = ''\n                if ($c.content -is [string]) {\n                    $content = $c.content\n                } else {\n                    $content = ($c.content | ConvertTo-Json -Compress -Depth 5)\n                }\n                if ($content.Length -gt 240) { $content = $content.Substring(0, 240) + '...' }\n                return [pscustomobject]@{\n                    text = '  -> ' + $content\n                    color = 'DarkGreen'\n                }\n            }\n        }\n        'result' {\n            return [pscustomobject]@{\n                text = '==> Done. Duration: ' + $ev.duration_ms + 'ms'\n                color = 'Cyan'\n                blankLineBefore = $true\n            }\n        }\n    }\n\n    return $null\n}\n", "Convert-ClaudeVisibleCliEvent", "\n$script:allTextBuilder = New-Object System.Text.StringBuilder\n$script:lastTextBlockBuilder = New-Object System.Text.StringBuilder\n$script:currentTextBlockBuilder = $null\n$script:currentContentBlockType = ''\n", "\n$fallbackText = $script:allTextBuilder.ToString()\nif ($script:lastTextBlockBuilder.Length -gt 0) {\n    $fallbackText = $script:lastTextBlockBuilder.ToString()\n}\nif (-not (Test-Path -LiteralPath $rawOutputPath) -and -not [string]::IsNullOrWhiteSpace($fallbackText)) {\n    [System.IO.File]::WriteAllText($rawOutputPath, $fallbackText, $utf8NoBom)\n}\n");
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static ClaudeCodeCliProvider GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
