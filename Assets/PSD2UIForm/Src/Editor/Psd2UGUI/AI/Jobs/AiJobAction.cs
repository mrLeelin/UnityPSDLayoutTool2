using System.Threading;
using UGF.EditorTools.Psd2UGUI;

namespace AiJobActionNamespace
{
    internal delegate void AiJobAction(AiJobContext context, CancellationToken cancellationToken);
}
