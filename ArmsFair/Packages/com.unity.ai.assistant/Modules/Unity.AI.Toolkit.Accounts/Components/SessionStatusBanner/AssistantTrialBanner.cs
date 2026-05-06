using Unity.AI.Toolkit.Accounts.Services.Core;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Components
{
    [UxmlElement]
    partial class AssistantTrialBanner : BasicBannerContent
    {
        public AssistantTrialBanner()
            : base(
                "Get from idea to innovation quickly. Let Unity AI automate repetitive setup and troubleshooting workflows, so you can focus on the creative elements of your game.",
                "Start the 14-day trial", AccountLinks.StartTrial,
                "Subscribe to Unity AI", AccountLinks.Subscribe) {}
    }
}
