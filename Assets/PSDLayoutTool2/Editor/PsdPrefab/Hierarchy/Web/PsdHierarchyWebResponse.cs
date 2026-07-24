namespace PsdLayoutTool2.Editor
{
    using System;
    using System.Text;

    internal sealed class PsdHierarchyWebResponse
    {
        private PsdHierarchyWebResponse(int statusCode, string contentType, byte[] body)
        {
            this.statusCode = statusCode;
            this.contentType = contentType;
            this.body = body ?? new byte[0];
        }

        public int statusCode { get; private set; }
        public string contentType { get; private set; }
        public byte[] body { get; private set; }

        public static PsdHierarchyWebResponse Json(string json)
        {
            return Json(200, json);
        }

        public static PsdHierarchyWebResponse Json(int statusCode, string json)
        {
            return new PsdHierarchyWebResponse(statusCode, "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(json ?? string.Empty));
        }

        public static PsdHierarchyWebResponse Png(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return new PsdHierarchyWebResponse(200, "image/png", copy);
        }

        public static PsdHierarchyWebResponse Asset(string contentType, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("Content type is required.", nameof(contentType));
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return new PsdHierarchyWebResponse(200, contentType, copy);
        }

        public static PsdHierarchyWebResponse Empty(int statusCode)
        {
            return new PsdHierarchyWebResponse(statusCode, "text/plain; charset=utf-8", new byte[0]);
        }
    }
}
