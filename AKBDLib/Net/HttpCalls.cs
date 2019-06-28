using System.Net.Http;

namespace AKBDLib.Net
{
    public static class HttpCalls
    {
        public static string GetString(string fromUrl)
        {
            using (var client = new HttpClient())
            using (var response = client.GetAsync(fromUrl).Result)
            using (var content = response.Content)
            {
                return content.ReadAsStringAsync().Result;
            }
        }
    }
}
