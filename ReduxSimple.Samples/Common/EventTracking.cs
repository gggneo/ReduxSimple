﻿using Microsoft.AppCenter.Analytics;
using System.Collections.Generic;
using System.Linq;

namespace ReduxSimple.Uwp.Samples.Common
{
    public static class EventTracking
    {
        public static void TrackNavigation(string from, string to)
        {
            Analytics.TrackEvent("Navigate", new Dictionary<string, string> { { "From", from }, { "To", to } });
        }

        public static void TrackReduxAction(object action, bool trackProperties = true)
        {
            var type = action.GetType();

            if (trackProperties)
            {
                var typeProperties = type.GetProperties();
                var properties = typeProperties
                    .Select(typeProperty => new { Key = typeProperty.Name, Value = typeProperty.GetValue(action).ToString() })
                    .ToDictionary(x => x.Key, x => x.Value);

                Analytics.TrackEvent(type.Name, properties);
            }
            else
            {
                Analytics.TrackEvent(type.Name);
            }
        }
    }
}
