using UnityEngine;

namespace Rlyeh.Localizer;

// Diagnostic builds only: change the row policy, then re-enter the native story.
internal static class ScenarioProbe
{
    internal static void Tick()
    {
        if (!Input.GetKeyDown(KeyCode.F9)) return;
        var scenarios = Plugin.Instance.Scenarios;
        scenarios.SetEnabled(!scenarios.Enabled);
        Plugin.Instance.Log.LogInfo("SCENARIO_PROBE enabled=" + scenarios.Enabled);
    }
}
