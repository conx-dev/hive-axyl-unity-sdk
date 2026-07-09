using System.Collections.Generic;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class ListMailResult
    {
        public IReadOnlyList<Mail> Mail { get; private set; }
        public string NextPageToken { get; private set; }
        public long Total { get; private set; }

        internal ListMailResult(IReadOnlyList<Mail> mail, string nextPageToken, long total)
        {
            Mail = mail;
            NextPageToken = nextPageToken;
            Total = total;
        }
    }

    public sealed class CheckNewMailResult
    {
        public bool HasNewMail { get; private set; }

        internal CheckNewMailResult(bool hasNewMail)
        {
            HasNewMail = hasNewMail;
        }
    }

    public sealed class MailboxApi
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

        public async Task<ListMailResult> ListMailAsync(
            int pageSize = 20,
            string pageToken = "",
            bool includeClaimed = false)
        {
            ConnectClient activeClient = RequireClient();
            string selectedLanguage = CurrentLanguage();
            ListMailRequest request = new ListMailRequest
            {
                Page = new PageRequest
                {
                    PageSize = pageSize,
                    PageToken = pageToken ?? ""
                },
                IncludeClaimed = includeClaimed
            };
            ListMailResponse response =
                await activeClient.UnaryAsync(
                    "MailboxService",
                    "ListMail",
                    request,
                    ListMailResponse.Parser,
                    true);
            List<Mail> mail = new List<Mail>();
            for (int i = 0; i < response.Mail.Count; i++)
            {
                mail.Add(Mail.From(response.Mail[i], selectedLanguage));
            }
            string nextPageToken = response.Page == null ? "" : response.Page.NextPageToken;
            long total = response.Page == null ? 0 : response.Page.Total;
            return new ListMailResult(mail, nextPageToken, total);
        }

        public async Task<CheckNewMailResult> CheckNewMailAsync()
        {
            ConnectClient activeClient = RequireClient();
            CheckNewMailRequest request = new CheckNewMailRequest();
            CheckNewMailResponse response =
                await activeClient.UnaryAsync(
                    "MailboxService",
                    "CheckNewMail",
                    request,
                    CheckNewMailResponse.Parser,
                    true);
            return new CheckNewMailResult(response.HasNewMail);
        }

        public async Task<Mail> ClaimMailAsync(string mailId)
        {
            ConnectClient activeClient = RequireClient();
            string selectedLanguage = CurrentLanguage();
            ClaimMailRequest request = new ClaimMailRequest
            {
                MailId = mailId
            };
            ClaimMailResponse response =
                await activeClient.UnaryAsync(
                    "MailboxService",
                    "ClaimMail",
                    request,
                    ClaimMailResponse.Parser,
                    true);
            if (response.Mail == null)
            {
                throw HiveAxylException.Transport("claim response missing mail");
            }
            return Mail.From(response.Mail, selectedLanguage);
        }

        private ConnectClient RequireClient()
        {
            lock (gate)
            {
                if (client == null)
                {
                    throw HiveAxylException.Transport("discovery returned no endpoint for domain: mailbox");
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
