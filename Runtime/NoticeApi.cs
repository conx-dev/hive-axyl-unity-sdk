using System.Collections.Generic;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class NoticeApi
    {
        private readonly object gate = new object();
        private ConnectClient client;
        private string language = "";

        internal void Bind(ConnectClient client, string language)
        {
            lock (gate)
            {
                this.client = client;
                this.language = language ?? "";
            }
        }

        internal void Unbind()
        {
            lock (gate)
            {
                client = null;
                language = "";
            }
        }

        public async Task<IReadOnlyList<Notice>> ListActiveNoticesAsync()
        {
            ConnectClient activeClient = RequireClient();
            string selectedLanguage = CurrentLanguage();
            ListActiveNoticesRequest request = new ListActiveNoticesRequest();
            ListActiveNoticesResponse response =
                await activeClient.UnaryAsync(
                    "NoticeService",
                    "ListActiveNotices",
                    request,
                    ListActiveNoticesResponse.Parser,
                    true);
            List<Notice> notices = new List<Notice>();
            for (int i = 0; i < response.Notices.Count; i++)
            {
                notices.Add(Notice.From(response.Notices[i], selectedLanguage));
            }
            return notices;
        }

        private ConnectClient RequireClient()
        {
            lock (gate)
            {
                if (client == null)
                {
                    throw HiveAxylException.Transport("discovery returned no endpoint for domain: notice");
                }
                return client;
            }
        }

        private string CurrentLanguage()
        {
            lock (gate)
            {
                return language;
            }
        }
    }
}
