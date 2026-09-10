using AiPatchOperationKindNamespace;

namespace AiPatchOperationParserNamespace
{
    internal sealed class AiPatchOperationParser
    {
        internal static AiPatchOperationParser s_ObfuscationSentinel;

        internal static bool TryParse(object text, out AiPatchOperationKind result)
        {
            switch (((string)(text ?? string.Empty)).Trim().ToLowerInvariant())
            {
            case "create_group":
                result = (AiPatchOperationKind)1;
                return true;
            case "flatten_group":
                result = (AiPatchOperationKind)3;
                return true;
            case "set_ui_type":
                result = (AiPatchOperationKind)4;
                return true;
            case "rename_node":
                result = (AiPatchOperationKind)5;
                return true;
            case "delete_generated_group":
                result = (AiPatchOperationKind)6;
                return true;
            default:
                result = (AiPatchOperationKind)0;
                return false;
            case "move_node":
                result = (AiPatchOperationKind)2;
                return true;
            }
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static AiPatchOperationParser GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
