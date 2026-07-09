using System;
using System.Threading.Tasks;
using Hiveng.V1;

namespace HiveAxyl.Sdk
{
    public sealed class PaymentApi
    {
        private static readonly long[] GrantPollDelaysMillis = { 1000L, 2000L, 3000L, 5000L };
        private readonly object gate = new object();
        private ConnectClient client;

        internal void Bind(ConnectClient client)
        {
            lock (gate)
            {
                this.client = client;
            }
        }

        internal void Unbind()
        {
            lock (gate)
            {
                client = null;
            }
        }

        public async Task<PaymentPurchase> GetPurchaseAsync(string purchaseId)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                throw HiveAxylException.InvalidArgument("purchaseId is required");
            }
            ConnectClient activeClient = RequireClient();
            PaymentServiceGetPurchaseRequest request = new PaymentServiceGetPurchaseRequest
            {
                PurchaseId = purchaseId
            };
            PaymentServiceGetPurchaseResponse response =
                await activeClient.UnaryAsync(
                    "PaymentService",
                    "GetPurchase",
                    request,
                    PaymentServiceGetPurchaseResponse.Parser,
                    true);
            if (response.Purchase == null)
            {
                throw HiveAxylException.Transport("get purchase response missing purchase");
            }
            return PaymentPurchase.From(response.Purchase);
        }

        public async Task<PaymentPurchase> WaitForPaymentGrantAsync(
            string purchaseId,
            long timeoutMillis = 30000L)
        {
            if (string.IsNullOrEmpty(purchaseId))
            {
                throw HiveAxylException.InvalidArgument("purchaseId is required");
            }
            if (timeoutMillis <= 0)
            {
                throw HiveAxylException.InvalidArgument("timeoutMillis must be positive");
            }
            DateTime startedAt = DateTime.UtcNow;
            PaymentPurchase purchase = await GetPurchaseAsync(purchaseId);
            if (IsGrantFinished(purchase) || IsPurchaseFailed(purchase))
            {
                return purchase;
            }
            int delayIndex = 0;
            long elapsed = ElapsedMillis(startedAt);
            while (elapsed < timeoutMillis)
            {
                long remainingMillis = timeoutMillis - elapsed;
                long delayMillis = Math.Min(NextDelayMillis(delayIndex), remainingMillis);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMillis));
                purchase = await GetPurchaseAsync(purchaseId);
                if (IsGrantFinished(purchase) || IsPurchaseFailed(purchase))
                {
                    return purchase;
                }
                delayIndex += 1;
                elapsed = ElapsedMillis(startedAt);
            }
            return purchase;
        }

        private ConnectClient RequireClient()
        {
            lock (gate)
            {
                if (client == null)
                {
                    throw HiveAxylException.Transport("discovery returned no endpoint for domain: payment");
                }
                return client;
            }
        }

        private static long NextDelayMillis(int index)
        {
            if (index < GrantPollDelaysMillis.Length)
            {
                return GrantPollDelaysMillis[index];
            }
            return GrantPollDelaysMillis[GrantPollDelaysMillis.Length - 1];
        }

        private static long ElapsedMillis(DateTime startedAt)
        {
            return (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
        }

        private static bool IsGrantFinished(PaymentPurchase purchase)
        {
            return purchase.GrantStatus == "delivered" || purchase.GrantStatus == "not_required";
        }

        private static bool IsPurchaseFailed(PaymentPurchase purchase)
        {
            switch (purchase.Status)
            {
                case "failed":
                case "canceled":
                case "refunded":
                case "expired":
                    return true;
                default:
                    return false;
            }
        }
    }
}
