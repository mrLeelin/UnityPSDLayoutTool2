using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UGF.EditorTools.Psd2UGUI;
using AiCliProviderBaseNamespace;

namespace OpenCodeCliProviderNamespace
{
    internal sealed class OpenCodeCliProvider : AiCliProviderBase
    {
        private static OpenCodeCliProvider s_ObfuscationSentinel;

        [SpecialName]
        public override string GetProviderId()
        {
            return "opencode-cli";
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
            return "opencode";
        }

        protected override string BuildCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "run", "--format", "json", "--dangerously-skip-permissions", "--no-replay" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("--dir");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            if (analysisRequest.ImageInputPaths != null)
            {
                for (int i = 0; i < analysisRequest.ImageInputPaths.Length; i++)
                {
                    string text = analysisRequest.ImageInputPaths[i];
                    if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                    {
                        list.Add("--file");
                        AiCliProviderBase.AddQuotedArgument(list, text);
                    }
                }
            }
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleCommandArguments(AiAnalysisRequest analysisRequest)
        {
            List<string> list = new List<string> { "run", "--format", "json", "--dangerously-skip-permissions", "--no-replay", "--thinking" };
            if (!string.IsNullOrWhiteSpace(analysisRequest.WorkingDirectory))
            {
                list.Add("--dir");
                AiCliProviderBase.AddQuotedArgument(list, analysisRequest.WorkingDirectory);
            }
            if (analysisRequest.ImageInputPaths != null)
            {
                for (int i = 0; i < analysisRequest.ImageInputPaths.Length; i++)
                {
                    string text = analysisRequest.ImageInputPaths[i];
                    if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                    {
                        list.Add("--file");
                        AiCliProviderBase.AddQuotedArgument(list, text);
                    }
                }
            }
            return AiCliProviderBase.JoinArguments(list);
        }

        protected override string BuildVisibleRunnerInvocation(string commandArguments, AiJobContext jobContext)
        {
            return BuildVisibleCliAdapterScript(commandArguments, "\nfunction Convert-OpenCodeVisibleCliEvent {\n    param([string]$Line)\n    if ([string]::IsNullOrWhiteSpace($Line)) { return $null }\n\n    try { $ev = $Line | ConvertFrom-Json -ErrorAction Stop } catch {\n        return [pscustomobject]@{ text = $Line; color = 'DarkGray' }\n    }\n\n    $type = ''\n    if ($ev.PSObject.Properties['type']) {\n        $type = [string]$ev.type\n    }\n\n    $part = $null\n    if ($ev.PSObject.Properties['part']) {\n        $part = $ev.part\n    }\n\n    $partType = ''\n    if ($null -ne $part -and $part.PSObject.Properties['type']) {\n        $partType = [string]$part.type\n    }\n\n    $partText = ''\n    if ($null -ne $part -and $part.PSObject.Properties['text']) {\n        $partText = [string]$part.text\n    }\n\n    switch ($type) {\n        'step_start' {\n            return [pscustomobject]@{\n                text = '==> Phase: running task'\n                color = 'Cyan'\n                blankLineBefore = $true\n                dedupeKey = 'phase'\n                dedupeValue = 'running task'\n            }\n        }\n        'step_finish' {\n            $summary = 'step completed'\n            if ($null -ne $part -and $part.PSObject.Properties['tokens'] -and $null -ne $part.tokens) {\n                $outputTokens = 0\n                if ($part.tokens.PSObject.Properties['output']) {\n                    $outputTokens = [int]$part.tokens.output\n                }\n\n                $reasoningTokens = 0\n                if ($part.tokens.PSObject.Properties['reasoning']) {\n                    $reasoningTokens = [int]$part.tokens.reasoning\n                }\n                if ($outputTokens > 0 -and $reasoningTokens > 0) {\n                    $summary = 'step completed (output=' + $outputTokens + ', reasoning=' + $reasoningTokens + ')'\n                } elseif ($outputTokens > 0) {\n                    $summary = 'step completed (output=' + $outputTokens + ')'\n                }\n            }\n\n            return [pscustomobject]@{\n                text = '==> Result: ' + $summary\n                color = 'Cyan'\n                blankLineBefore = $true\n            }\n        }\n        'reasoning' {\n            if ([string]::IsNullOrWhiteSpace($partText)) { return $null }\n            return @(\n                [pscustomobject]@{\n                    text = '--- Thinking ---'\n                    color = 'DarkYellow'\n                    blankLineBefore = $true\n                    dedupeKey = 'section'\n                    dedupeValue = 'thinking'\n                },\n                [pscustomobject]@{\n                    text = $partText\n                    color = 'DarkYellow'\n                }\n            )\n        }\n        'text' {\n            if ([string]::IsNullOrWhiteSpace($partText)) { return $null }\n            return @(\n                [pscustomobject]@{\n                    text = '--- Response ---'\n                    color = 'White'\n                    blankLineBefore = $true\n                    dedupeKey = 'section'\n                    dedupeValue = 'response'\n                },\n                [pscustomobject]@{\n                    text = $partText\n                    color = 'White'\n                }\n            )\n        }\n        'error' {\n            $errorText = 'opencode error'\n            if (-not [string]::IsNullOrWhiteSpace($partText)) {\n                $errorText = $partText\n            } elseif ($ev.PSObject.Properties['message']) {\n                $errorText = [string]$ev.message\n            }\n            return [pscustomobject]@{\n                text = 'ERROR: ' + $errorText\n                color = 'Red'\n                blankLineBefore = $true\n            }\n        }\n    }\n\n    if ($partType -eq 'tool' -or $partType -eq 'tool-call') {\n        $toolText = $partText\n        if ([string]::IsNullOrWhiteSpace($toolText)) {\n            $toolText = $partType\n        }\n        return [pscustomobject]@{\n            text = '[Tool] ' + $toolText\n            color = 'Green'\n            blankLineBefore = $true\n        }\n    }\n\n    return [pscustomobject]@{\n        text = $Line\n        color = 'DarkGray'\n    }\n}\n", "Convert-OpenCodeVisibleCliEvent");
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static OpenCodeCliProvider GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
