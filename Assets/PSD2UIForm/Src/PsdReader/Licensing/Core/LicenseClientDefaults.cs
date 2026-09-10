using cn.efunstudio.psdreader;

namespace LicenseClientDefaultsNamespace
{
    internal sealed class LicenseClientDefaults
    {
        private static LicenseClientDefaults s_ObfuscationSentinel;

        internal static PsdReaderLicenseClientConfigDocument CreateDefaultConfig()
        {
            PsdReaderLicenseClientConfigDocument psdReaderLicenseClientConfigDocument = new PsdReaderLicenseClientConfigDocument();
            psdReaderLicenseClientConfigDocument.VendorCode = "efunstudio";
            psdReaderLicenseClientConfigDocument.ProductCode = "psd2ugui";
            psdReaderLicenseClientConfigDocument.RepositoryBaseUrls = new string[2] { "https://gitee.com/e-funny/efunstudio/raw/master", "https://gitcode.com/efunstudio/protector/raw/master" };
            psdReaderLicenseClientConfigDocument.MatomoUrl = "https://matomo.efunstudio.cn/matomo/matomo.php";
            psdReaderLicenseClientConfigDocument.MatomoSiteId = "1";
            psdReaderLicenseClientConfigDocument.RequestTimeoutSeconds = 60;
            psdReaderLicenseClientConfigDocument.CurrentMajorVersion = PsdReaderVersion.CurrentMajor;
            return psdReaderLicenseClientConfigDocument;
        }

        internal static string GetEmbeddedPublicKeyPem()
        {
            return string.Join("\n", "-----BEGIN PUBLIC KEY-----", "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAuHLKXeCuTRBbk+dJ6wPS", "YC8pZM13jehqRPb/HONzy5zZ/Q0Fll28217Po206OKzJS4ZHMtRTYKQstZOHGwnM", "N1H9ylE3nuM6qzOsUf9R8VV4yzYleXK+dTXJUewiujCtKT5XBeT0jUU/+0ytIrMB", "bg4/m2WlluY7Yloa8bahgzMrathZjH5DSvr4BfY95zK4SdvAiXdhX0jEkLRUJEIx", "/Ye3h6A7pG7NS9rm9O7hotoYktvuVZHHN3+1KnCGg4VcIMrzwD3xb3NmqyyPDmj0", "VLn6RQtGeym+vEz+zbqeaAalfss/UBXX3G9Se77guFoG3dTOco23g7xQ9Yck6+Dy", "XaD14i1iAr6sOg+0fPd85yqyct8e3i7aTwtFpawQk95MScGcbSv/qDRC33sHa//f", "+DfOd5V3uwZMQrOgBZoYisM66gOAWskOCJ6q8e3YgxdmxS6UTOJNvuUK5c9P2uVz", "7wKBwNG6HfU9p0R0SvHgmv4FyeaxuCZfXO4XP64PcbVJAgMBAAE=", "-----END PUBLIC KEY-----", string.Empty);
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static LicenseClientDefaults GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
