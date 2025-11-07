using System.Net;
using System.Reflection;

namespace KamiYomu.Web.Extensions
{
    public static class UriExtensions
    {
        public static bool IsValidImageUri(this Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }
            var validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".svg" };
            var extension = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
            return validExtensions.Contains(extension);
        }

        public static string GetFileNameFromUri(this Uri uri)
        {
            return Path.GetFileName(uri.LocalPath);
        }

        public static string GetContentType(this Uri uri)
        {
            var extension = Path.GetExtension(uri.LocalPath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }

        public static string ToEncodedString(this Uri uri)
        {
            return WebUtility.UrlEncode(uri.ToString());
        }

        public static Uri ToInternalImageUrl(this Uri uri)
        {
            if (!uri.IsValidImageUri()) return uri;

            return new Uri($"/Libraries/Collection/Index?handler=Image&uri={uri.ToEncodedString()}", UriKind.Relative);
        }



    }
}
