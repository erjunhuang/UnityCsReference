// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityEditor.Profiling
{
    // With the addition of Profiler Counters, users can create modules than use counters from the built-in ProfilerAreas. Therefore we use a reference count to manage the areas in use and only disable one when no modules are using it.
    internal class ProfilerAreaReferenceCounter
    {
        const int k_InvalidIndex = -1;

        int[] areaReferenceCounts;

        public ProfilerAreaReferenceCounter()
        {
            var count = Enum.GetNames(typeof(ProfilerArea)).Length;
            areaReferenceCounts = new int[count];

            // Profiler areas are enabled and capturing by default. Disable them initially and only enable once reference counted.
            DisableAllAreas();
        }

        public void IncrementArea(ProfilerArea area)
        {
            var index = IndexForArea(area);
            if (index == k_InvalidIndex)
            {
                return;
            }

            var referenceCount = areaReferenceCounts[index];
            bool wasZeroBeforeIncrement = (referenceCount == 0);
            areaReferenceCounts[index] = referenceCount + 1;

            if (wasZeroBeforeIncrement)
            {
                SetAreaEnabled(area, true);
            }
        }

        public void DecrementArea(ProfilerArea area)
        {
            var index = IndexForArea(area);
            if (index == k_InvalidIndex)
            {
                return;
            }

            var referenceCount = areaReferenceCounts[index];
            areaReferenceCounts[index] = Mathf.Max(referenceCount - 1, 0);

            bool isZero = areaReferenceCounts[index] == 0;
            if (isZero)
            {
                SetAreaEnabled(area, false);
            }
        }

        void SetAreaEnabled(ProfilerArea area, bool enabled)
        {
            // The CPU area should not be explicitly enabled or disabled as that would set Profiler.enabled.
            if (area == ProfilerArea.CPU)
                return;

            ProfilerDriver.SetAreaEnabled(area, enabled);
        }

        void DisableAllAreas()
        {
            for (int i = 0; i < areaReferenceCounts.Length; i++)
            {
                var area = (ProfilerArea)i;
                SetAreaEnabled(area, false);
            }
        }

        int IndexForArea(ProfilerArea area)
        {
            var index = (int)area;
            if (index < 0 || index >= areaReferenceCounts.Length)
            {
                index = k_InvalidIndex;
            }
            return index;
        }
    }

    internal static class ProfilerAreaReferenceCounterUtility
    {
        static readonly Dictionary<string, IEnumerable<ProfilerArea>> k_ProfilerCategoriesToAreasMap = new Dictionary<string, IEnumerable<ProfilerArea>>()
        {
            { ProfilerCategory.Render.Name, new ProfilerArea[] { ProfilerArea.Rendering } },
            { ProfilerCategory.Memory.Name, new ProfilerArea[] { ProfilerArea.Memory } },
            { ProfilerCategory.Audio.Name, new ProfilerArea[] { ProfilerArea.Audio } },
            { ProfilerCategory.Video.Name, new ProfilerArea[] { ProfilerArea.Video } },
            // Both Physics and Physics2D modules will remain active if any counter from the Physics category is active.
            { ProfilerCategory.Physics.Name, new ProfilerArea[] { ProfilerArea.Physics, ProfilerArea.Physics2D } },
            // Both NetworkMessages and NetworkOperations modules will remain active if any counter from the Network category is active.
            { ProfilerCategory.Network.Name, new ProfilerArea[] { ProfilerArea.NetworkMessages, ProfilerArea.NetworkOperations } },
            // Both UI and UIDetails modules will remain active if any counter from the GUI category is active.
            { ProfilerCategory.Gui.Name, new ProfilerArea[] { ProfilerArea.UI, ProfilerArea.UIDetails } },
            { ProfilerCategory.Lighting.Name, new ProfilerArea[] { ProfilerArea.GlobalIllumination } },
            { ProfilerCategory.VirtualTexturing.Name, new ProfilerArea[] { ProfilerArea.VirtualTexturing } },
        };

        public static IEnumerable<ProfilerArea> ProfilerCategoryNameToAreas(string categoryName)
        {
            // Map a counter's category to on or more built-in Profiler areas. This is part of the transition away from ProfilerArea and to Category/Counter pairs. A counter's category potentially needs to be mapped to a ProfilerArea in order to ensure that an area is not disabled when a module may be using counters associated with the legacy ProfilerArea. It is expected a category that does not correspond to any ProfilerArea returns an empty collection. It is expected that a category that corresponds to many ProfilerAreas returns all corresponding ProfilerAreas. No category corresponds to the CPU or GPU areas. Therefore, all ProfilerAreas should be accounted for here minus CPU and GPU. This is asserted in the test ProfilerAreaReferenceCounterUtilityTests_HandlesEveryBuiltInProfilerAreaExceptCPUAndGPU.

            return k_ProfilerCategoriesToAreasMap.TryGetValue(categoryName, out var areas) ? areas : new ProfilerArea[0];
        }
    }
}
