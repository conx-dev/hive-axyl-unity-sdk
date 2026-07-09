using Hiveng.V1;
using UnityEngine;

namespace HiveAxyl.Sdk
{
    internal static class UnityClientPlatform
    {
        public static ClientPlatform ToProto(HiveAxylClientPlatform platform)
        {
            switch (platform)
            {
                case HiveAxylClientPlatform.Web:
                    return ClientPlatform.Web;
                case HiveAxylClientPlatform.Android:
                    return ClientPlatform.Android;
                case HiveAxylClientPlatform.Ios:
                    return ClientPlatform.Ios;
                case HiveAxylClientPlatform.Desktop:
                    return ClientPlatform.Desktop;
                default:
                    return Detect();
            }
        }

        private static ClientPlatform Detect()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return ClientPlatform.Android;
                case RuntimePlatform.IPhonePlayer:
                    return ClientPlatform.Ios;
                case RuntimePlatform.WebGLPlayer:
                    return ClientPlatform.Web;
                default:
                    return ClientPlatform.Desktop;
            }
        }
    }
}
