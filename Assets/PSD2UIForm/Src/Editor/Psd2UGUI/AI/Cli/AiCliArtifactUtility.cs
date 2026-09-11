using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AiJobFileStoreNamespace;
using AiPatchOperationParserNamespace;
using AiResultDocumentKindNamespace;
using UGF.EditorTools.Psd2UGUI;
using UnityEngine;
using Object = UnityEngine.Object;
using AiCliEventKindNamespace;
using AiPatchOperationKindNamespace;

namespace AiCliArtifactUtilityNamespace
{
    internal sealed class AiCliArtifactUtility
    {
        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly Regex _jsonStringPropertyRegex = new Regex("\"(?<key>[^\"]+)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _forbiddenAliasFieldRegex = new Regex("\"(?<key>target|nodeId|siblingIndex|judgement|mainType|roleType|currentMainType|currentRoleType|predictedMainType|predictedRoleType)\"\\s*:", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _logLineRegex = new Regex("^(?<time>\\d{4}-\\d{2}-\\d{2}T\\S+)\\s+(?<level>[A-Z]+)\\s+(?<body>.+)$", RegexOptions.Compiled);

        private static readonly Regex _itemTypeRegex = new Regex("\"item\"\\s*:\\s*\\{.*?\"type\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _itemStatusRegex = new Regex("\"item\"\\s*:\\s*\\{.*?\"status\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _itemCommandRegex = new Regex("\"item\"\\s*:\\s*\\{.*?\"command\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _textDeltaRegex = new Regex("\"delta\"\\s*:\\s*\\{.*?\"type\"\\s*:\\s*\"text_delta\".*?\"text\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _assistantTextRegex = new Regex("\"type\"\\s*:\\s*\"text\"\\s*,\\s*\"text\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _resultTextRegex = new Regex("\"result\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _apiErrorStatusRegex = new Regex("\"api_error_status\"\\s*:\\s*(?<value>\\d+)", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex _windowsJsonPathRegex = new Regex("(?<path>[A-Za-z]:\\\\(?:[^\\\\/:*?\"<>|\\r\\n]+\\\\)*[^\\\\/:*?\"<>|\\r\\n]+\\.json(?:\\.tmp)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _unixJsonPathRegex = new Regex("(?<path>/(?:[^/\\r\\n\"'`]+/)*[^/\\r\\n\"'`]+\\.json(?:\\.tmp)?)", RegexOptions.Compiled);

        internal static AiCliArtifactUtility s_ObfuscationSentinel;

        internal static string BuildPrompt(object text2, object aiAnalysisRequest)
        {
            if (!string.IsNullOrWhiteSpace((string)text2) && File.Exists((string)text2))
            {
                string value = ReadTextWithSharedAccess(text2);
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }
                StringBuilder stringBuilder = new StringBuilder(value);
                if (aiAnalysisRequest != null && ((AiAnalysisRequest)aiAnalysisRequest).PromptTags != null && ((AiAnalysisRequest)aiAnalysisRequest).PromptTags.Length != 0)
                {
                    for (int i = 0; i < ((AiAnalysisRequest)aiAnalysisRequest).PromptTags.Length; i++)
                    {
                        AiPromptTag aiPromptTag = ((AiAnalysisRequest)aiAnalysisRequest).PromptTags[i];
                        if (aiPromptTag != null && !string.IsNullOrWhiteSpace(aiPromptTag.key))
                        {
                            stringBuilder.Replace(aiPromptTag.key, aiPromptTag.value ?? string.Empty);
                        }
                    }
                }
                if (aiAnalysisRequest != null && ((AiAnalysisRequest)aiAnalysisRequest).RequireExplicitOutputJsonFile)
                {
                    string text = BuildOutputContract(aiAnalysisRequest);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        stringBuilder.Insert(0, text + Environment.NewLine + Environment.NewLine);
                    }
                }
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        internal static string ReadTextWithSharedAccess(object text)
        {
            if (!string.IsNullOrWhiteSpace((string)text) && File.Exists((string)text))
            {
                using (FileStream stream = new FileStream((string)text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    using StreamReader streamReader = new StreamReader(stream, _utf8NoBom, detectEncodingFromByteOrderMarks: true);
                    return streamReader.ReadToEnd();
                }
            }
            return string.Empty;
        }

        internal static bool TryValidateResultFile(object aiAnalysisRequest, out string result)
        {
            result = null;
            if (aiAnalysisRequest == null)
            {
                result = "AI request is null.";
                return false;
            }
            string text = GetExistingResultPath(aiAnalysisRequest);
            if ((!string.IsNullOrWhiteSpace(text) && File.Exists(text)) || TryRecoverResultFileFromReferencedPaths(aiAnalysisRequest, out text, out var _))
            {
                if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                {
                    string text3;
                    try
                    {
                        text3 = AiJobFileStore.ReadTextIfExists(text);
                    }
                    catch (IOException ex)
                    {
                        result = "Result JSON file is not ready yet: " + ex.Message;
                        return false;
                    }
                    return TryValidateResultJson(((AiAnalysisRequest)aiAnalysisRequest).ResultDocumentKind, text3, out result);
                }
                result = "Result JSON file not found.";
                return false;
            }
            result = "Result JSON file not found.";
            return false;
        }

        internal static bool TryRecoverResultJsonFromProviderOutput(object json, object aiAnalysisRequest, out string result)
        {
            result = null;
            if (aiAnalysisRequest == null)
            {
                result = "AI request is null.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace((string)json))
            {
                string text = ExtractResultJson(json, ((AiAnalysisRequest)aiAnalysisRequest).ResultDocumentKind);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (TryValidateResultJson(((AiAnalysisRequest)aiAnalysisRequest).ResultDocumentKind, text, out result))
                    {
                        try
                        {
                            AiJobFileStore.WriteTextAtomic(GetWritableResultPath(aiAnalysisRequest), text);
                            return true;
                        }
                        catch (Exception ex)
                        {
                            result = "Failed to persist JSON output: " + ex.Message;
                            return false;
                        }
                    }
                    return false;
                }
                result = "Could not extract JSON object from provider output.";
                return false;
            }
            result = "Provider output is empty.";
            return false;
        }

        internal static bool TryFinalizeResultFile(object value, out string result)
        {
            return TryFinalizeResultFileWithRecoveryOption(value, true, out result);
        }

        internal static bool TryFinalizeResultFileWithRecoveryOption(object aiAnalysisRequest, bool enabled, out string result)
        {
            result = null;
            if (aiAnalysisRequest != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiAnalysisRequest)aiAnalysisRequest).OutputJsonPath))
                {
                    string text = GetExistingResultPath(aiAnalysisRequest);
                    if ((!string.IsNullOrWhiteSpace(text) && File.Exists(text)) || (enabled && TryRecoverResultFileFromReferencedPaths(aiAnalysisRequest, out text, out result)))
                    {
                        if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
                        {
                            string text2;
                            try
                            {
                                text2 = AiJobFileStore.ReadTextIfExists(text);
                            }
                            catch (IOException ex)
                            {
                                result = "Result JSON file is not ready yet: " + ex.Message;
                                return false;
                            }
                            if (!TryValidateResultJson(((AiAnalysisRequest)aiAnalysisRequest).ResultDocumentKind, text2, out result))
                            {
                                return false;
                            }
                            try
                            {
                                if (!AreSamePath(text, ((AiAnalysisRequest)aiAnalysisRequest).OutputJsonPath))
                                {
                                    AiJobFileStore.WriteTextAtomic(((AiAnalysisRequest)aiAnalysisRequest).OutputJsonPath, text2);
                                    AiJobFileStore.DeleteFileIfExists(text);
                                }
                                return true;
                            }
                            catch (Exception ex2)
                            {
                                result = "Failed to commit JSON output: " + ex2.Message;
                                return false;
                            }
                        }
                        result = "Result JSON file not found.";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        result = "Result JSON file not found.";
                    }
                    return false;
                }
                result = "Final result JSON path is empty.";
                return false;
            }
            result = "AI request is null.";
            return false;
        }

        internal static bool TryRecoverAssistantTextFromStreamFile(object text3, out string result, out string result2)
        {
            result = string.Empty;
            result2 = null;
            if (!string.IsNullOrWhiteSpace((string)text3) && File.Exists((string)text3))
            {
                string text;
                try
                {
                    text = ReadTextWithSharedAccess(text3);
                }
                catch (IOException ex)
                {
                    result2 = "Stream output file is not ready yet: " + ex.Message;
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    StringBuilder stringBuilder = new StringBuilder(text.Length / 8);
                    string[] array = text.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string text2 in array)
                    {
                        if (string.IsNullOrWhiteSpace(text2))
                        {
                            continue;
                        }
                        string a = GetJsonStringProperty(text2, "type");
                        if (!string.Equals(a, "stream_event", StringComparison.Ordinal))
                        {
                            if (!string.Equals(a, "assistant", StringComparison.Ordinal))
                            {
                                if (string.Equals(a, "result", StringComparison.Ordinal))
                                {
                                    AppendRegexValues(stringBuilder, _resultTextRegex, text2);
                                }
                            }
                            else
                            {
                                AppendRegexValues(stringBuilder, _assistantTextRegex, text2);
                            }
                        }
                        else
                        {
                            AppendRegexValues(stringBuilder, _textDeltaRegex, text2);
                        }
                    }
                    result = stringBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        result2 = "Could not recover assistant text from stream output.";
                        return false;
                    }
                    return true;
                }
                result2 = "Stream output is empty.";
                return false;
            }
            result2 = "Stream output file not found.";
            return false;
        }

        internal static bool TryBuildFailureMessage(object value, object value2, object value3, out string result)
        {
            result = string.Empty;
            if (!TryExtractFailureMessageFromFile(value, out result))
            {
                if (TryExtractFailureMessageFromText(value3, out result))
                {
                    return true;
                }
                if (!TryExtractFailureMessageFromText(ReadTextIfExistsSafe(value2), out result))
                {
                    return false;
                }
                return true;
            }
            return true;
        }

        internal static bool IsLikelyResultJson(AiResultDocumentKind value, object json)
        {
            if (!string.IsNullOrWhiteSpace((string)json))
            {
                string text = ((string)json).Trim();
                int num = text.IndexOf('{');
                int num2 = text.LastIndexOf('}');
                if (num >= 0 && num2 > num)
                {
                    string text2 = text.Substring(num, num2 - num + 1);
                    if (!text2.Contains("\"treeHash\"", StringComparison.Ordinal))
                    {
                        return false;
                    }
                    switch (value)
                    {
                    default:
                        return false;
                    case (AiResultDocumentKind)1:
                        return text2.Contains("\"nodes\"", StringComparison.Ordinal);
                    case (AiResultDocumentKind)2:
                        return text2.Contains("\"relations\"", StringComparison.Ordinal);
                    case (AiResultDocumentKind)3:
                        return text2.Contains("\"operations\"", StringComparison.Ordinal);
                    case (AiResultDocumentKind)4:
                        if (text2.Contains("\"analysis\"", StringComparison.Ordinal))
                        {
                            return true;
                        }
                        return text2.Contains("\"operations\"", StringComparison.Ordinal);
                    case (AiResultDocumentKind)5:
                        if (!text2.Contains("\"owners\"", StringComparison.Ordinal) || !text2.Contains("\"roles\"", StringComparison.Ordinal))
                        {
                            return false;
                        }
                        return text2.Contains("\"nodeLabels\"", StringComparison.Ordinal);
                    }
                }
                return false;
            }
            return false;
        }

        private static string GetExistingResultPath(object value)
        {
            if (value != null)
            {
                if (!string.IsNullOrWhiteSpace(((AiAnalysisRequest)value).OutputTempJsonPath) && File.Exists(((AiAnalysisRequest)value).OutputTempJsonPath))
                {
                    return ((AiAnalysisRequest)value).OutputTempJsonPath;
                }
                return ((AiAnalysisRequest)value).OutputJsonPath ?? string.Empty;
            }
            return string.Empty;
        }

        private static string BuildOutputContract(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            string text = ((!string.IsNullOrWhiteSpace(((AiAnalysisRequest)value).OutputTempJsonPath)) ? ((AiAnalysisRequest)value).OutputTempJsonPath : ((AiAnalysisRequest)value).OutputJsonPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }
            StringBuilder stringBuilder = new StringBuilder(512);
            stringBuilder.AppendLine("## CLI Output Contract");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("- 你必须把最终 " + GetResultDocumentDescription(((AiAnalysisRequest)value).ResultDocumentKind) + " 写入下面这个 UTF-8 文件路径：");
            stringBuilder.AppendLine("  " + text);
            stringBuilder.AppendLine("- 这个文件是 Unity 认定任务成功的唯一产物。");
            stringBuilder.AppendLine("- 不要写入 `recognition_output.json` 或任何其它自定义结果文件。");
            stringBuilder.AppendLine("- 最终 assistant 回复必须与该文件内容完全一致，且只能是一个 JSON 对象。");
            stringBuilder.AppendLine("- 不要输出总结、进度汇报、Markdown、代码块或额外说明。");
            stringBuilder.AppendLine("- 如果需要自检或中间处理，只能放在 thinking/tool 事件里，不能放在最终 assistant 回复里。");
            return stringBuilder.ToString().TrimEnd();
        }

        private static bool TryRecoverResultFileFromReferencedPaths(object value, out string result, out string result2)
        {
            result = string.Empty;
            result2 = null;
            if (value != null)
            {
                string text = GetWritableResultPath(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string workingDirectory = ((AiAnalysisRequest)value).WorkingDirectory;
                    if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                    {
                        string text2 = ReadFileTail(((AiAnalysisRequest)value).OutputRawTextPath, 262144);
                        string text3 = ReadFileTail(((AiAnalysisRequest)value).StreamOutputPath, 262144);
                        List<string> list = new List<string>(8);
                        CollectJsonPaths(list, text2);
                        CollectJsonPaths(list, text3);
                        string text4 = null;
                        for (int i = 0; i < list.Count; i++)
                        {
                            string text5 = list[i];
                            if (!IsPathWithinDirectory(text5, workingDirectory) || !File.Exists(text5))
                            {
                                continue;
                            }
                            string text6;
                            try
                            {
                                text6 = AiJobFileStore.ReadTextIfExists(text5);
                            }
                            catch (IOException ex)
                            {
                                text4 = "Recovered JSON file is not ready yet: " + ex.Message;
                                continue;
                            }
                            if (!TryValidateResultJson(((AiAnalysisRequest)value).ResultDocumentKind, text6, out text4))
                            {
                                continue;
                            }
                            try
                            {
                                if (!AreSamePath(text5, text))
                                {
                                    AiJobFileStore.WriteTextAtomic(text, text6);
                                }
                                result = GetExistingResultPath(value);
                                if (string.IsNullOrWhiteSpace(result))
                                {
                                    result = text;
                                }
                                return true;
                            }
                            catch (Exception ex2)
                            {
                                text4 = "Failed to recover JSON output: " + ex2.Message;
                            }
                        }
                        result2 = text4;
                        return false;
                    }
                    return false;
                }
                result2 = "Writable result JSON path is empty.";
                return false;
            }
            result2 = "AI request is null.";
            return false;
        }

        private static void CollectJsonPaths(List<string> texts, object value)
        {
            CollectJsonPathsMatchingRegex(texts, value, _windowsJsonPathRegex);
            CollectJsonPathsMatchingRegex(texts, value, _unixJsonPathRegex);
        }

        private static void CollectJsonPathsMatchingRegex(List<string> texts, object value2, object value3)
        {
            if (texts == null || value3 == null || string.IsNullOrWhiteSpace((string)value2))
            {
                return;
            }
            MatchCollection matchCollection = ((Regex)value3).Matches((string)value2);
            for (int i = 0; i < matchCollection.Count; i++)
            {
                Match match = matchCollection[i];
                if (!match.Success)
                {
                    continue;
                }
                string value = match.Groups["path"].Value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(value.Trim());
                }
                catch
                {
                    continue;
                }
                bool flag = false;
                for (int j = 0; j < texts.Count; j++)
                {
                    if (string.Equals(texts[j], fullPath, GetPathComparison()))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    texts.Add(fullPath);
                }
            }
        }

        private static string GetWritableResultPath(object value)
        {
            if (value != null)
            {
                string text;
                if (string.IsNullOrWhiteSpace(((AiAnalysisRequest)value).OutputTempJsonPath))
                {
                    text = ((AiAnalysisRequest)value).OutputJsonPath;
                    if (text == null)
                    {
                        return string.Empty;
                    }
                }
                else
                {
                    text = ((AiAnalysisRequest)value).OutputTempJsonPath;
                }
                return text;
            }
            return string.Empty;
        }

        private static bool IsPathWithinDirectory(object value2, object value3)
        {
            if (string.IsNullOrWhiteSpace((string)value2) || string.IsNullOrWhiteSpace((string)value3))
            {
                return false;
            }
            string fullPath;
            string fullPath2;
            try
            {
                fullPath = Path.GetFullPath((string)value2);
                fullPath2 = Path.GetFullPath((string)value3);
            }
            catch
            {
                return false;
            }
            if (!string.Equals(fullPath, fullPath2, GetPathComparison()))
            {
                string value = fullPath2.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullPath.StartsWith(value, GetPathComparison());
            }
            return true;
        }

        private static bool AreSamePath(object value, object value2)
        {
            return string.Equals(Path.GetFullPath((string)(value ?? string.Empty)), Path.GetFullPath((string)(value2 ?? string.Empty)), GetPathComparison());
        }

        private static StringComparison GetPathComparison()
        {
            if ((int)Application.platform != 7)
            {
                return StringComparison.Ordinal;
            }
            return StringComparison.OrdinalIgnoreCase;
        }

        internal static bool TryValidateResultJson(AiResultDocumentKind value, object json, out string result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace((string)json))
            {
                result = "JSON output is empty.";
                return false;
            }
            switch (value)
            {
            default:
                result = "Unsupported AI result document kind.";
                return false;
            case (AiResultDocumentKind)1:
                return TryValidateMainTypeJson(json, out result);
            case (AiResultDocumentKind)2:
                return TryValidateChildRelationJson(json, out result);
            case (AiResultDocumentKind)3:
                return TryValidateStructuralJson(json, out result);
            case (AiResultDocumentKind)4:
                return TryValidatePatchJson(json, out result);
            case (AiResultDocumentKind)5:
                return TryValidateCombinedRecognitionJson(json, out result);
            }
        }

        internal static bool TryValidatePatchJson(object json, out string result)
        {
            result = null;
            if (!string.IsNullOrWhiteSpace((string)json))
            {
                Match match = _forbiddenAliasFieldRegex.Match((string)json);
                if (match.Success)
                {
                    result = "Patch JSON contains forbidden alias field '" + match.Groups["key"].Value + "'.";
                    return false;
                }
                try
                {
                    AiPatchDocument aiPatchDocument = JsonUtility.FromJson<AiPatchDocument>((string)json);
                    if (aiPatchDocument == null || string.IsNullOrWhiteSpace(aiPatchDocument.treeHash))
                    {
                        result = "Patch JSON is missing treeHash.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    result = "Patch JSON parse failed: " + ex.Message;
                    return false;
                }
                return true;
            }
            result = "Patch JSON is empty.";
            return false;
        }

        internal static bool TryParseCliEvent(object text3, out AiCliEventKind result, out string result2)
        {
            result = (AiCliEventKind)0;
            result2 = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)text3))
            {
                string text = ((string)text3).Trim();
                if (text.Length < 2 || text[0] != '{')
                {
                    return false;
                }
                string text2 = GetJsonStringProperty(text, "type");
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    switch (text2)
                    {
                    case "response.completed":
                    case "turn.completed":
                    case "session.completed":
                        result = (AiCliEventKind)1;
                        result2 = GetJsonStringProperty(text, "message") ?? GetJsonStringProperty(text, "summary") ?? text2;
                        return true;
                    case "error":
                    case "response.failed":
                    case "session.failed":
                    case "turn.failed":
                        result = (AiCliEventKind)2;
                        result2 = GetJsonStringProperty(text, "message") ?? GetJsonStringProperty(text, "content") ?? GetJsonStringProperty(text, "title") ?? text2;
                        return true;
                    default:
                        return false;
                    }
                }
                return false;
            }
            return false;
        }

        internal static string ExtractVisibleDisplayText(object text10)
        {
            if (string.IsNullOrWhiteSpace((string)text10))
            {
                return string.Empty;
            }
            string text = ((string)text10).Trim();
            if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(5).Trim();
            }
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                if (text[0] == '{')
                {
                    if (text.IndexOf("\"targetId\"", StringComparison.Ordinal) >= 0 || text.IndexOf("\"predictedUIType\"", StringComparison.Ordinal) >= 0 || text.IndexOf("\"ownerId\"", StringComparison.Ordinal) >= 0 || text.IndexOf("\"roleType\"", StringComparison.Ordinal) >= 0 || text.IndexOf("\"memberNodeIds\"", StringComparison.Ordinal) >= 0 || text.IndexOf("\"operations\"", StringComparison.Ordinal) >= 0 || (text.IndexOf("\"op\"", StringComparison.Ordinal) >= 0 && text.IndexOf("\"uiType\"", StringComparison.Ordinal) >= 0))
                    {
                        return string.Empty;
                    }
                    string text2 = GetJsonStringProperty(text, "type");
                    string text3 = GetJsonStringProperty(text, "reasoning_content") ?? GetJsonStringProperty(text, "content") ?? GetJsonStringProperty(text, "text") ?? GetJsonStringProperty(text, "message") ?? GetJsonStringProperty(text, "summary") ?? GetJsonStringProperty(text, "title");
                    if (!string.IsNullOrWhiteSpace(text3))
                    {
                        return NormalizeSingleLineText(text3);
                    }
                    if (!string.IsNullOrWhiteSpace(text2) && text2.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
                    {
                        string text4 = ExtractRegexValue(_itemTypeRegex, text);
                        string text5 = ExtractRegexValue(_itemStatusRegex, text);
                        string text6 = ExtractRegexValue(_itemCommandRegex, text);
                        if (!string.IsNullOrWhiteSpace(text4) || !string.IsNullOrWhiteSpace(text5))
                        {
                            string text7 = (string.IsNullOrWhiteSpace(text4) ? text2 : text4);
                            if (!string.IsNullOrWhiteSpace(text5))
                            {
                                text7 = text7 + " (" + text5 + ")";
                            }
                            string text8 = SimplifyCommandText(text6);
                            if (!string.IsNullOrWhiteSpace(text8))
                            {
                                text7 = text7 + ": " + text8;
                            }
                            return NormalizeSingleLineText("AI任务进程: " + text7);
                        }
                    }
                    string text9 = text2 ?? GetJsonStringProperty(text, "event") ?? GetJsonStringProperty(text, "subtype") ?? GetJsonStringProperty(text, "status");
                    if (!string.IsNullOrWhiteSpace(text9))
                    {
                        return NormalizeSingleLineText("AI任务进程: " + text9);
                    }
                }
                Match match = _logLineRegex.Match(text);
                if (match.Success)
                {
                    string value = match.Groups["body"].Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return NormalizeSingleLineText("AI任务进程: " + value);
                    }
                }
                return NormalizeSingleLineText(text);
            }
            return "AI 输出完成，正在整理结果...";
        }

        internal static string GetResultDocumentDescription(AiResultDocumentKind value)
        {
            return value switch
            {
                (AiResultDocumentKind)1 => "主控件识别结果 JSON", 
                (AiResultDocumentKind)2 => "子控件关系识别结果 JSON", 
                (AiResultDocumentKind)3 => "结构修复计划 JSON", 
                (AiResultDocumentKind)4 => "最终 patch JSON", 
                (AiResultDocumentKind)5 => "UI元素识别结果 JSON", 
                _ => "最终 JSON 结果", 
            };
        }

        private static bool TryValidateMainTypeJson(object value, out string result)
        {
            result = null;
            try
            {
                AiMainTypeResultDocument aiMainTypeResultDocument = JsonUtility.FromJson<AiMainTypeResultDocument>((string)value);
                if (aiMainTypeResultDocument != null)
                {
                    if (string.IsNullOrWhiteSpace(aiMainTypeResultDocument.treeHash))
                    {
                        result = "MainType JSON is missing treeHash.";
                        return false;
                    }
                    if (aiMainTypeResultDocument.nodes != null)
                    {
                        return true;
                    }
                    result = "MainType JSON is missing nodes.";
                    return false;
                }
                result = "MainType JSON is invalid.";
                return false;
            }
            catch (Exception ex)
            {
                result = "MainType JSON parse failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryValidateCombinedRecognitionJson(object value, out string result)
        {
            result = null;
            try
            {
                AiRecognitionCombinedResultDocument aiRecognitionCombinedResultDocument = JsonUtility.FromJson<AiRecognitionCombinedResultDocument>((string)value);
                if (aiRecognitionCombinedResultDocument == null)
                {
                    result = "RecognitionCombined JSON is invalid.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(aiRecognitionCombinedResultDocument.treeHash))
                {
                    result = "RecognitionCombined JSON is missing treeHash.";
                    return false;
                }
                if (aiRecognitionCombinedResultDocument.owners != null && aiRecognitionCombinedResultDocument.roles != null && aiRecognitionCombinedResultDocument.nodeLabels != null)
                {
                    return true;
                }
                result = "RecognitionCombined JSON must contain owners, roles, and nodeLabels.";
                return false;
            }
            catch (Exception ex)
            {
                result = "RecognitionCombined JSON parse failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryValidateChildRelationJson(object value, out string result)
        {
            result = null;
            try
            {
                AiChildRelationResultDocument aiChildRelationResultDocument = JsonUtility.FromJson<AiChildRelationResultDocument>((string)value);
                if (aiChildRelationResultDocument != null)
                {
                    if (string.IsNullOrWhiteSpace(aiChildRelationResultDocument.treeHash))
                    {
                        result = "ChildRelation JSON is missing treeHash.";
                        return false;
                    }
                    if (aiChildRelationResultDocument.relations == null)
                    {
                        result = "ChildRelation JSON is missing relations.";
                        return false;
                    }
                    return true;
                }
                result = "ChildRelation JSON is invalid.";
                return false;
            }
            catch (Exception ex)
            {
                result = "ChildRelation JSON parse failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryValidateStructuralJson(object value, out string result)
        {
            result = null;
            try
            {
                AiStructuralResultDocument aiStructuralResultDocument = JsonUtility.FromJson<AiStructuralResultDocument>((string)value);
                if (aiStructuralResultDocument != null)
                {
                    if (!string.IsNullOrWhiteSpace(aiStructuralResultDocument.treeHash))
                    {
                        if (aiStructuralResultDocument.operations == null)
                        {
                            result = "Structural JSON is missing operations.";
                            return false;
                        }
                        for (int i = 0; i < aiStructuralResultDocument.operations.Count; i++)
                        {
                            AiPatchOperation aiPatchOperation = aiStructuralResultDocument.operations[i];
                            if (aiPatchOperation != null)
                            {
                                if (!AiPatchOperationParser.TryParse(aiPatchOperation.op, out var value2) || (value2 != (AiPatchOperationKind)1 && value2 != (AiPatchOperationKind)2 && value2 != (AiPatchOperationKind)4))
                                {
                                    result = $"Structural operation[{i}] has unsupported op '{aiPatchOperation.op}'.";
                                    return false;
                                }
                                continue;
                            }
                            result = $"Structural operation[{i}] is null.";
                            return false;
                        }
                        return true;
                    }
                    result = "Structural JSON is missing treeHash.";
                    return false;
                }
                result = "Structural JSON is invalid.";
                return false;
            }
            catch (Exception ex)
            {
                result = "Structural JSON parse failed: " + ex.Message;
                return false;
            }
        }

        private static string ExtractResultJson(object value, AiResultDocumentKind value2)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return null;
            }
            string text = StripMarkdownCodeFence(((string)value).Trim());
            List<string> list = new List<string>(8);
            AddUniqueCandidate(list, text);
            AddUniqueCandidate(list, ExtractOuterJsonObject(text));
            List<string> list2 = ExtractJsonObjects(text);
            for (int i = 0; i < list2.Count; i++)
            {
                AddUniqueCandidate(list, list2[i]);
            }
            int num = 0;
            string text2;
            while (true)
            {
                if (num < list.Count)
                {
                    text2 = list[num];
                    if (TryValidateResultJson(value2, text2, out var _))
                    {
                        break;
                    }
                    num++;
                    continue;
                }
                if (list.Count > 0)
                {
                    return list[0];
                }
                return null;
            }
            return text2;
        }

        private static void AppendRegexValues(object value2, object value3, object value4)
        {
            if (value2 == null || value3 == null || string.IsNullOrWhiteSpace((string)value4))
            {
                return;
            }
            MatchCollection matchCollection = ((Regex)value3).Matches((string)value4);
            for (int i = 0; i < matchCollection.Count; i++)
            {
                Match match = matchCollection[i];
                if (match.Success)
                {
                    string value = Regex.Unescape(match.Groups["value"].Value);
                    if (!string.IsNullOrEmpty(value))
                    {
                        ((StringBuilder)value2).Append(value);
                    }
                }
            }
        }

        private static string StripMarkdownCodeFence(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Trim();
                if (text.StartsWith("```", StringComparison.Ordinal))
                {
                    int num = text.IndexOf('\n');
                    if (num >= 0)
                    {
                        text = text.Substring(num + 1).Trim();
                    }
                    int num2 = text.LastIndexOf("```", StringComparison.Ordinal);
                    if (num2 >= 0)
                    {
                        text = text.Substring(0, num2).Trim();
                    }
                    return text;
                }
                return text;
            }
            return string.Empty;
        }

        private static string ExtractOuterJsonObject(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                int num = ((string)value).IndexOf('{');
                int num2 = ((string)value).LastIndexOf('}');
                if (num < 0 || num2 < num)
                {
                    return null;
                }
                return ((string)value).Substring(num, num2 - num + 1).Trim();
            }
            return null;
        }

        private static List<string> ExtractJsonObjects(object value)
        {
            List<string> list = new List<string>(4);
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return list;
            }
            int num = 0;
            int num2 = -1;
            bool flag = false;
            bool flag2 = false;
            for (int i = 0; i < ((string)value).Length; i++)
            {
                char c = ((string)value)[i];
                if (flag2)
                {
                    flag2 = false;
                    continue;
                }
                switch (c)
                {
                case '\\':
                    flag2 = true;
                    continue;
                case '"':
                    flag = !flag;
                    continue;
                }
                if (flag)
                {
                    continue;
                }
                switch (c)
                {
                case '{':
                    if (num == 0)
                    {
                        num2 = i;
                    }
                    num++;
                    break;
                case '}':
                    if (num > 0)
                    {
                        num--;
                        if (num == 0 && num2 >= 0)
                        {
                            list.Add(((string)value).Substring(num2, i - num2 + 1).Trim());
                            num2 = -1;
                        }
                    }
                    break;
                }
            }
            return list;
        }

        private static void AddUniqueCandidate(List<string> texts, object value)
        {
            if (texts == null || string.IsNullOrWhiteSpace((string)value))
            {
                return;
            }
            string text = ((string)value).Trim();
            int num = 0;
            while (true)
            {
                if (num < texts.Count)
                {
                    if (!string.Equals(texts[num], text, StringComparison.Ordinal))
                    {
                        num++;
                        continue;
                    }
                    break;
                }
                texts.Add(text);
                break;
            }
        }

        private static bool TryExtractFailureMessageFromFile(object value, out string result)
        {
            result = string.Empty;
            string text = ReadFileTail(value, 65536);
            if (!string.IsNullOrWhiteSpace(text))
            {
                string[] array = text.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                int num = array.Length - 1;
                while (true)
                {
                    if (num >= 0)
                    {
                        if (TryExtractFailureMessageFromLine(array[num], out result))
                        {
                            break;
                        }
                        num--;
                        continue;
                    }
                    return TryExtractFailureMessageFromText(text, out result);
                }
                return true;
            }
            return false;
        }

        private static bool TryExtractFailureMessageFromLine(object value, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Trim();
                if (text.Length >= 2 && text[0] == '{')
                {
                    if (TryParseCliEvent(text, out var aiCliEventKind, out var text2) && aiCliEventKind == (AiCliEventKind)2 && TryExtractFailureMessageFromText(text2, out result))
                    {
                        return true;
                    }
                    string a = GetJsonStringProperty(text, "type");
                    bool flag = text.IndexOf("\"is_error\":true", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("\"error\":", StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(a, "error", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "result", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "assistant", StringComparison.OrdinalIgnoreCase);
                    string text3 = GetJsonStringProperty(text, "result") ?? GetJsonStringProperty(text, "message") ?? GetJsonStringProperty(text, "content") ?? GetJsonStringProperty(text, "title") ?? GetJsonStringProperty(text, "summary") ?? GetJsonStringProperty(text, "text");
                    if (string.IsNullOrWhiteSpace(text3) && string.Equals(a, "assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        text3 = ExtractAssistantContent(text);
                    }
                    if (!TryExtractFailureMessageFromText(text3, out result))
                    {
                        if (flag)
                        {
                            string text4 = ExtractApiErrorStatus(text);
                            if (!string.IsNullOrWhiteSpace(text4))
                            {
                                result = "API Error: " + text4 + " Request Error";
                                return true;
                            }
                        }
                        return false;
                    }
                    return true;
                }
                return TryExtractFailureMessageFromText(text, out result);
            }
            return false;
        }

        private static bool TryExtractFailureMessageFromText(object value, out string result)
        {
            result = string.Empty;
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Replace("\r", "\n").Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    string[] array = text.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int num = array.Length - 1;
                    string text2;
                    while (true)
                    {
                        if (num >= 0)
                        {
                            text2 = NormalizeSingleLineText(array[num]);
                            if (IsFailureMessage(text2))
                            {
                                break;
                            }
                            num--;
                            continue;
                        }
                        text = NormalizeSingleLineText(text);
                        if (IsFailureMessage(text))
                        {
                            result = text;
                            return true;
                        }
                        return false;
                    }
                    result = text2;
                    return true;
                }
                return false;
            }
            return false;
        }

        private static string ReadTextIfExistsSafe(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value) || !File.Exists((string)value))
            {
                return string.Empty;
            }
            try
            {
                return AiJobFileStore.ReadTextIfExists(value);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadFileTail(object value, int value2)
        {
            if (!string.IsNullOrWhiteSpace((string)value) && File.Exists((string)value) && value2 >= 1)
            {
                try
                {
                    using FileStream fileStream = new FileStream((string)value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    int num = (int)Math.Min(value2, fileStream.Length);
                    if (num >= 1)
                    {
                        fileStream.Seek(-num, SeekOrigin.End);
                        byte[] array = new byte[num];
                        int count = fileStream.Read(array, 0, num);
                        return _utf8NoBom.GetString(array, 0, count);
                    }
                    return string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        private static string ExtractAssistantContent(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                StringBuilder stringBuilder = new StringBuilder(256);
                AppendRegexValues(stringBuilder, _assistantTextRegex, value);
                return stringBuilder.ToString().Trim();
            }
            return string.Empty;
        }

        private static string ExtractApiErrorStatus(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return string.Empty;
            }
            Match match = _apiErrorStatusRegex.Match((string)value);
            if (match.Success)
            {
                return match.Groups["value"].Value;
            }
            return string.Empty;
        }

        private static string ExtractRegexValue(object value, object value2)
        {
            if (value != null && !string.IsNullOrWhiteSpace((string)value2))
            {
                Match match = ((Regex)value).Match((string)value2);
                if (!match.Success)
                {
                    return null;
                }
                return Regex.Unescape(match.Groups["value"].Value);
            }
            return null;
        }

        private static string GetJsonStringProperty(object value, object value2)
        {
            if (string.IsNullOrWhiteSpace((string)value) || string.IsNullOrWhiteSpace((string)value2))
            {
                return null;
            }
            MatchCollection matchCollection = _jsonStringPropertyRegex.Matches((string)value);
            for (int i = 0; i < matchCollection.Count; i++)
            {
                Match match = matchCollection[i];
                if (match.Success && string.Equals(match.Groups["key"].Value, (string)value2, StringComparison.OrdinalIgnoreCase))
                {
                    return Regex.Unescape(match.Groups["value"].Value);
                }
            }
            return null;
        }

        private static string SimplifyCommandText(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = NormalizeSingleLineText(value);
                int num = text.IndexOf("Get-Content", StringComparison.OrdinalIgnoreCase);
                if (num >= 0)
                {
                    return text.Substring(num);
                }
                int num2 = text.LastIndexOf('\\');
                if (num2 >= 0 && num2 + 1 < text.Length)
                {
                    return text.Substring(num2 + 1);
                }
                return text;
            }
            return string.Empty;
        }

        private static string NormalizeSingleLineText(object value)
        {
            if (!string.IsNullOrWhiteSpace((string)value))
            {
                string text = ((string)value).Replace("\r", " ").Replace("\n", " ").Trim();
                while (text.Contains("  "))
                {
                    text = text.Replace("  ", " ");
                }
                return text;
            }
            return string.Empty;
        }

        private static bool IsFailureMessage(object value)
        {
            if (string.IsNullOrWhiteSpace((string)value))
            {
                return false;
            }
            string text = ((string)value).Trim();
            if (text.Length >= 4)
            {
                if (string.Equals(text, "unknown", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "error", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                if (text.IndexOf("API Error", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("Request Error", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("failed", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("failure", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("too many requests", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("quota", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("permission denied", StringComparison.OrdinalIgnoreCase) < 0 && text.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return text.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return true;
            }
            return false;
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiCliArtifactUtility GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
