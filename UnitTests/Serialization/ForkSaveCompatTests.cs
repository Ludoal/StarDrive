using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ship_Game;
using Ship_Game.Data.Serialization;

namespace UnitTests.Serialization;

[TestClass]
public class ForkSaveCompatTests
{
    // A save written by this fork must stay readable by a stock build. A field the stock
    // build has never heard of is skipped harmlessly, but an unknown TYPE is not: only
    // enums can be read past without their type, and only on a build carrying that skip.
    // So an enum declared solely in this fork must never reach the save graph - store the
    // int and keep the enum in code. Adding a fork-only enum here breaks downstream saves.
    static readonly string[] ForkOnlyEnums =
    {
        "Ship_Game.CargoPriority",
        "Ship_Game.Planet+BuildMandate",
    };

    [TestMethod]
    public void ForkOnlyEnumsStayOutOfTheSaveGraph()
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                               | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var offenders = new List<string>();
        foreach (Type t in typeof(Empire).Assembly.GetTypes())
        {
            foreach (MemberInfo m in t.GetFields(All).Cast<MemberInfo>().Concat(t.GetProperties(All)))
            {
                if (m.GetCustomAttribute<StarDataAttribute>() == null)
                    continue;

                Type mt = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
                mt = Nullable.GetUnderlyingType(mt) ?? mt;
                string name = mt.FullName ?? mt.Name;
                if (ForkOnlyEnums.Contains(name))
                    offenders.Add($"{t.FullName}.{m.Name} is [StarData] {name}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A fork-only enum reached the save graph, which makes the save unreadable by a stock "
          + "build. Serialize the int and expose the enum as a plain property:\n  "
          + string.Join("\n  ", offenders));
    }
}
