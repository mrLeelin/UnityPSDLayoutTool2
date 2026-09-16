using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UGF.EditorTools.Psd2UGUI;
using AiCliProviderBaseNamespace;

namespace CodexCliProviderNamespace
{
    internal sealed class CodexCliProvider : AiCliProviderBase
    {
        internal static CodexCliProvider s_ObfuscationSentinel;

        internal CodexCliProvider(AiProviderConnectionSettings connectionSettings = null)
            : base(AiProviderKind.CodexCli, connectionSettings)
        {
        }

        [SpecialName]
        public override string GetProviderId()
        {
            return "codex-cli";
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
            return "codex";
        }

        protected override string BuildCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "-a", "never", "exec", "-s", "workspace-write", "--skip-git-repo-check", "--ephemeral", "--json" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("-C");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            if (analysisRequest.ImageInputPaths != null)
            {
                for (int i = 0; i < analysisRequest.ImageInputPaths.Length; i++)
                {
                    string text = analysisRequest.ImageInputPaths[i];
                    if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                    {
                        list.Add("-i");
                        AiCliProviderBase.AddQuotedArgument(list, text);
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(analysisRequest.OutputRawTextPath))
            {
                list.Add("-o");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.OutputRawTextPath);
            }
            list.Add("-");
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "-a", "never", "exec", "-s", "workspace-write", "--skip-git-repo-check", "--ephemeral", "--json" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("-C");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            if (analysisRequest.ImageInputPaths != null)
            {
                for (int i = 0; i < analysisRequest.ImageInputPaths.Length; i++)
                {
                    string text = analysisRequest.ImageInputPaths[i];
                    if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                    {
                        list.Add("-i");
                        AiCliProviderBase.AddQuotedArgument(list, text);
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(analysisRequest.OutputRawTextPath))
            {
                list.Add("-o");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.OutputRawTextPath);
            }
            list.Add("-");
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleRunnerInvocation(string commandArguments, AiJobContext jobContext)
        {
            return BuildVisibleCliAdapterScript(commandArguments, "\nfunction Normalize-CodexText {\n    param([string]$Text, [int]$Limit = 240)\n    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }\n\n    $normalized = $Text.Replace(\"`r\", ' ').Replace(\"`n\", ' ').Trim()\n    while ($normalized.Contains('  ')) {\n        $normalized = $normalized.Replace('  ', ' ')\n    }\n\n    if ($normalized.Length -gt $Limit) {\n        return $normalized.Substring(0, $Limit) + '...'\n    }\n\n    return $normalized\n}\n\nfunction Get-CodexObjectString {\n    param($Object, [string]$PropertyName)\n    if ($null -eq $Object -or [string]::IsNullOrWhiteSpace($PropertyName)) { return '' }\n\n    $property = $Object.PSObject.Properties[$PropertyName]\n    if ($null -eq $property -or $null -eq $property.Value) { return '' }\n\n    if ($property.Value -is [string]) {\n        return [string]$property.Value\n    }\n\n    return [string]$property.Value.ToString()\n}\n\nfunction Get-CodexPhaseForItemType {\n    param([string]$ItemType)\n    switch ($ItemType) {\n        'mcp_tool_call'  { return 'executing tools' }\n        'shell_command'  { return 'executing tools' }\n        'function_call'  { return 'executing tools' }\n        'reasoning'      { return 'reasoning' }\n        'agent_message'  { return 'assembling result' }\n        default          { return '' }\n    }\n}\n\nfunction Summarize-CodexCommand {\n    param([string]$CommandText)\n    if ([string]::IsNullOrWhiteSpace($CommandText)) { return '' }\n\n    $summary = Normalize-CodexText $CommandText 180\n    $getContentIndex = $summary.IndexOf('Get-Content', [System.StringComparison]::OrdinalIgnoreCase)\n    if ($getContentIndex -ge 0) {\n        return Normalize-CodexText $summary.Substring($getContentIndex) 180\n    }\n\n    return $summary\n}\n\nfunction Get-CodexItemLabel {\n    param($Item)\n    if ($null -eq $Item) { return '' }\n\n    $itemType = Get-CodexObjectString $Item 'type'\n    $commandText = Get-CodexObjectString $Item 'command'\n    if (-not [string]::IsNullOrWhiteSpace($commandText)) {\n        return Summarize-CodexCommand $commandText\n    }\n\n    $tool = Get-CodexObjectString $Item 'tool'\n    $server = Get-CodexObjectString $Item 'server'\n    $title = ''\n    if ($Item.PSObject.Properties['arguments'] -and $null -ne $Item.arguments) {\n        $title = Get-CodexObjectString $Item.arguments 'title'\n    }\n\n    if (-not [string]::IsNullOrWhiteSpace($title)) {\n        return Normalize-CodexText $title 180\n    }\n\n    if (-not [string]::IsNullOrWhiteSpace($tool)) {\n        $toolLabel = $tool\n        if (-not [string]::IsNullOrWhiteSpace($server)) {\n            $toolLabel = $server + '/' + $tool\n        }\n        return Normalize-CodexText $toolLabel 180\n    }\n\n    if ($itemType -eq 'agent_message') {\n        return 'assistant response'\n    }\n\n    if ($itemType -eq 'reasoning') {\n        return 'reasoning'\n    }\n\n    return Normalize-CodexText $itemType 180\n}\n\nfunction Get-CodexContentText {\n    param($Value)\n    if ($null -eq $Value) { return '' }\n\n    if ($Value -is [string]) {\n        return [string]$Value\n    }\n\n    if ($Value -is [System.Collections.IEnumerable]) {\n        foreach ($entry in $Value) {\n            if ($null -eq $entry) { continue }\n\n            $entryText = Get-CodexObjectString $entry 'text'\n            if (-not [string]::IsNullOrWhiteSpace($entryText)) {\n                return $entryText\n            }\n\n            $nestedContent = Get-CodexObjectString $entry 'content'\n            if (-not [string]::IsNullOrWhiteSpace($nestedContent)) {\n                return $nestedContent\n            }\n        }\n    }\n\n    return ''\n}\n\nfunction Get-CodexErrorText {\n    param($EventObject)\n    if ($null -eq $EventObject) { return '' }\n\n    foreach ($propertyName in @('message', 'summary', 'title', 'content', 'text')) {\n        $value = Get-CodexObjectString $EventObject $propertyName\n        if (-not [string]::IsNullOrWhiteSpace($value)) {\n            return Normalize-CodexText $value 320\n        }\n    }\n\n    if ($EventObject.PSObject.Properties['error'] -and $null -ne $EventObject.error) {\n        $errorText = Get-CodexObjectString $EventObject.error 'message'\n        if ([string]::IsNullOrWhiteSpace($errorText)) {\n            $errorText = Get-CodexObjectString $EventObject.error 'text'\n        }\n        if ([string]::IsNullOrWhiteSpace($errorText)) {\n            $errorText = [string]$EventObject.error\n        }\n        if (-not [string]::IsNullOrWhiteSpace($errorText)) {\n            return Normalize-CodexText $errorText 320\n        }\n    }\n\n    if ($EventObject.PSObject.Properties['item'] -and $null -ne $EventObject.item) {\n        $item = $EventObject.item\n\n        if ($item.PSObject.Properties['error'] -and $null -ne $item.error) {\n            $errorText = Get-CodexObjectString $item.error 'message'\n            if ([string]::IsNullOrWhiteSpace($errorText)) {\n                $errorText = Get-CodexObjectString $item.error 'text'\n            }\n            if ([string]::IsNullOrWhiteSpace($errorText)) {\n                $errorText = [string]$item.error\n            }\n            if (-not [string]::IsNullOrWhiteSpace($errorText)) {\n                return Normalize-CodexText $errorText 320\n            }\n        }\n\n        if ($item.PSObject.Properties['result'] -and $null -ne $item.result) {\n            $resultText = Get-CodexContentText $item.result.content\n            if (-not [string]::IsNullOrWhiteSpace($resultText)) {\n                return Normalize-CodexText $resultText 320\n            }\n        }\n    }\n\n    return ''\n}\n\nfunction Get-CodexResultText {\n    param($Item)\n    if ($null -eq $Item) { return '' }\n\n    $itemText = Get-CodexObjectString $Item 'text'\n    if (-not [string]::IsNullOrWhiteSpace($itemText)) {\n        $trimmed = $itemText.Trim()\n        if ($trimmed.StartsWith('{') -or $trimmed.StartsWith('[')) {\n            return 'assistant response generated'\n        }\n\n        return Normalize-CodexText $itemText 240\n    }\n\n    return 'assistant response generated'\n}\n\nfunction Get-CodexTurnResultSummary {\n    param($EventObject)\n    if ($null -eq $EventObject -or -not $EventObject.PSObject.Properties['usage'] -or $null -eq $EventObject.usage) {\n        return 'task completed'\n    }\n\n    $outputTokens = 0\n    $reasoningTokens = 0\n    if ($EventObject.usage.PSObject.Properties['output_tokens'] -and $null -ne $EventObject.usage.output_tokens) {\n        $outputTokens = [int]$EventObject.usage.output_tokens\n    }\n    if ($EventObject.usage.PSObject.Properties['reasoning_output_tokens'] -and $null -ne $EventObject.usage.reasoning_output_tokens) {\n        $reasoningTokens = [int]$EventObject.usage.reasoning_output_tokens\n    }\n\n    if ($reasoningTokens > 0 -and $outputTokens > 0) {\n        return ('task completed (output=' + $outputTokens + ', reasoning=' + $reasoningTokens + ')')\n    }\n    if ($outputTokens > 0) {\n        return ('task completed (output=' + $outputTokens + ')')\n    }\n    return 'task completed'\n}\n\nfunction Convert-CodexVisibleCliEvent {\n    param([object]$Chunk)\n    if ($null -eq $Chunk) { return $null }\n\n    $line = [string]$Chunk\n    if ([string]::IsNullOrWhiteSpace($line)) { return $null }\n\n    $trimmed = $line.Trim()\n    if ($trimmed.Length -lt 2 -or $trimmed[0] -ne '{') {\n        return [pscustomobject]@{ text = $line; color = 'DarkGray' }\n    }\n\n    try {\n        $ev = $trimmed | ConvertFrom-Json -ErrorAction Stop\n    }\n    catch {\n        return [pscustomobject]@{ text = $line; color = 'DarkGray' }\n    }\n\n    $type = [string]$ev.type\n\n    switch ($type) {\n        'thread.started' {\n            return [pscustomobject]@{\n                text = '==> Codex session started'\n                color = 'Cyan'\n                blankLineBefore = $true\n                dedupeKey = 'session'\n                dedupeValue = 'codex-thread'\n            }\n        }\n        'turn.started' {\n            return [pscustomobject]@{\n                text = '==> Phase: running task'\n                color = 'Cyan'\n                blankLineBefore = $true\n                dedupeKey = 'phase'\n                dedupeValue = 'running task'\n            }\n        }\n        'turn.completed' {\n            return [pscustomobject]@{\n                text = '==> Result: ' + (Get-CodexTurnResultSummary $ev)\n                color = 'Cyan'\n                blankLineBefore = $true\n            }\n        }\n        'turn.failed' {\n            $detail = Get-CodexErrorText $ev\n            if ([string]::IsNullOrWhiteSpace($detail)) { $detail = 'task failed' }\n            return [pscustomobject]@{\n                text = 'ERROR: ' + $detail\n                color = 'Red'\n                blankLineBefore = $true\n            }\n        }\n        'session.started' {\n            return [pscustomobject]@{\n                text = '==> Codex session started'\n                color = 'Cyan'\n                blankLineBefore = $true\n                dedupeKey = 'session'\n                dedupeValue = 'codex-session'\n            }\n        }\n        'session.completed' {\n            return [pscustomobject]@{\n                text = '==> Result: session completed'\n                color = 'Cyan'\n                blankLineBefore = $true\n            }\n        }\n        'session.failed' {\n            $detail = Get-CodexErrorText $ev\n            if ([string]::IsNullOrWhiteSpace($detail)) { $detail = 'session failed' }\n            return [pscustomobject]@{\n                text = 'ERROR: ' + $detail\n                color = 'Red'\n                blankLineBefore = $true\n            }\n        }\n        'error' {\n            $detail = Get-CodexErrorText $ev\n            if ([string]::IsNullOrWhiteSpace($detail)) { $detail = 'unexpected codex error' }\n            return [pscustomobject]@{\n                text = 'ERROR: ' + $detail\n                color = 'Red'\n            }\n        }\n    }\n\n    if ($type.StartsWith('item.', [System.StringComparison]::OrdinalIgnoreCase) -and $null -ne $ev.item) {\n        $itemType = Get-CodexObjectString $ev.item 'type'\n        $itemStatus = Get-CodexObjectString $ev.item 'status'\n        $itemLabel = Get-CodexItemLabel $ev.item\n        $phase = Get-CodexPhaseForItemType $itemType\n\n        $events = New-Object System.Collections.Generic.List[object]\n        if (-not [string]::IsNullOrWhiteSpace($phase)) {\n            $events.Add([pscustomobject]@{\n                text = '==> Phase: ' + $phase\n                color = 'Cyan'\n                blankLineBefore = $true\n                dedupeKey = 'phase'\n                dedupeValue = $phase\n            })\n        }\n\n        if ($itemStatus -eq 'failed' -or $type.EndsWith('.failed', [System.StringComparison]::OrdinalIgnoreCase)) {\n            $detail = Get-CodexErrorText $ev\n            if ([string]::IsNullOrWhiteSpace($detail)) {\n                $detail = 'item failed'\n                if (-not [string]::IsNullOrWhiteSpace($itemLabel)) {\n                    $detail = $itemLabel\n                }\n            } elseif (-not [string]::IsNullOrWhiteSpace($itemLabel)) {\n                $detail = $itemLabel + ': ' + $detail\n            }\n            $events.Add([pscustomobject]@{\n                text = 'ERROR: ' + $detail\n                color = 'Red'\n            })\n            return $events\n        }\n\n        if ($itemType -eq 'agent_message' -and $type.EndsWith('.completed', [System.StringComparison]::OrdinalIgnoreCase)) {\n            $events.Add([pscustomobject]@{\n                text = '[Result] ' + (Get-CodexResultText $ev.item)\n                color = 'White'\n            })\n            return $events\n        }\n\n        if ([string]::IsNullOrWhiteSpace($itemLabel)) {\n            $itemLabel = $type\n            if (-not [string]::IsNullOrWhiteSpace($itemType)) {\n                $itemLabel = $itemType\n            }\n        }\n\n        if ($type.EndsWith('.started', [System.StringComparison]::OrdinalIgnoreCase)) {\n            $events.Add([pscustomobject]@{\n                text = '[Item] ' + $itemLabel\n                color = 'Green'\n            })\n            return $events\n        }\n\n        if ($type.EndsWith('.completed', [System.StringComparison]::OrdinalIgnoreCase)) {\n            $events.Add([pscustomobject]@{\n                text = '[Item] Done: ' + $itemLabel\n                color = 'DarkGreen'\n            })\n            return $events\n        }\n\n        $events.Add([pscustomobject]@{\n            text = '[Item] ' + $itemLabel\n            color = 'Green'\n        })\n        return $events\n    }\n\n    $fallback = Get-CodexErrorText $ev\n    if (-not [string]::IsNullOrWhiteSpace($fallback)) {\n        return [pscustomobject]@{\n            text = $fallback\n            color = 'DarkYellow'\n        }\n    }\n\n    return [pscustomobject]@{\n        text = $line\n        color = 'DarkGray'\n    }\n}\n", "Convert-CodexVisibleCliEvent");
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static CodexCliProvider GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
