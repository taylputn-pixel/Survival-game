using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GrandmasHouse.Tests.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Not a test: a whole first year played by a scripted player, several
    // times over, to see how the food, wood and hens hold up. Run it on its
    // own (group GrandmasHouse.Tests.SimulatedYear); it's left out of a full
    // run. Writes Logs/YearSim/days.csv and summary.csv, and logs a table.
    [Explicit("A long simulation, not a check. Run it on its own.")]
    public class SimulatedYear
    {
        const int Seeds = 5;

        [TearDown]
        public void PutSavesBack() => Homestead.RestoreRealSaves();

        [UnityTest]
        [Timeout(3600000)]
        public IEnumerator ThreeKindsOfPlayer()
        {
            var results = new List<Run>();
            foreach (var policy in Policy.All)
            {
                for (int i = 0; i < Seeds; i++)
                {
                    Homestead.UseMemorySaves();
                    yield return Homestead.Load(canningSetUp: true, hens: false);
                    var run = new Run(policy, 1000 + i * 7919);
                    yield return run.Play();
                    results.Add(run);
                }
            }
            string folder = Path.Combine(Application.dataPath, "..", "Logs", "YearSim");
            Directory.CreateDirectory(folder);
            var days = new StringBuilder(Run.DayHeader + "\n");
            var summary = new StringBuilder(Run.SummaryHeader + "\n");
            foreach (var r in results)
            {
                foreach (var row in r.Days) days.Append(row).Append('\n');
                summary.Append(r.Summary()).Append('\n');
            }
            File.WriteAllText(Path.Combine(folder, "days.csv"), days.ToString());
            File.WriteAllText(Path.Combine(folder, "summary.csv"), summary.ToString());
            Debug.Log("[YearSim]\n" + Run.Table(results) + "\nCSV in " + Path.GetFullPath(folder));
        }
    }
}
