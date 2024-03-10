using System.Reflection;

using LabFusion.SDK.Gamemodes;

using MelonLoader;

namespace HideAndSeek
{
    internal partial class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            GamemodeRegistration.LoadGamemodes(Assembly.GetExecutingAssembly());
        }
    }
}
