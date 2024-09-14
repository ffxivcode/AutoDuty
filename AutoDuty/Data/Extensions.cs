using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace AutoDuty.Data
{
    public static class Extensions
    {
        public static bool StartsWithIgnoreCase(this string str, string strsw) => str.StartsWith(strsw, StringComparison.OrdinalIgnoreCase);

        public static string ToCustomString(this PathAction pathAction) 
        {
            string argumentString = string.Empty;

            foreach (var argument in pathAction.Arguments.Select((Value, Index) => (Value, Index)))
                argumentString += (argument.Index + 1) < pathAction.Arguments.Count ? $"{argument.Value}," : $"{argument.Value}";

            return $"{pathAction.Name}|{pathAction.Position.ToCustomString()}|{argumentString}|{pathAction.Note}"; 
        }

        public static string ToCustomString(this Vector3 vector3) => $"{vector3.X:F2}, {vector3.Y:F2}, {vector3.Z:F2}";

        public static List<string> ToConditional(this string conditionalString)
        {
            var list = new List<string>();
            var firstParse = conditionalString.Replace(" ", string.Empty).Split('(');
            if (firstParse.Length < 2) return list;
            var secondparse = firstParse[1].Split(")");
            if (secondparse.Length < 2) return list;
            var method = firstParse[0];
            var argument = secondparse[0];
            string? operatorValue;
            string? rightSideValue;
            if (secondparse[1][1] == '=')
            {
                operatorValue = $"{secondparse[1][0]}{secondparse[1][1]}";
                rightSideValue = secondparse[1].Replace(operatorValue, string.Empty);
            }
            else
            {
                operatorValue = $"{secondparse[1][0]}";
                rightSideValue = secondparse[1].Replace(operatorValue, string.Empty);
            }
            list.Add(method, argument, operatorValue, rightSideValue);
            return list;
        }


        public static bool TryToGetVector3(this string vector3String, out Vector3 vector3)
        {
            vector3 = Vector3.Zero;
            var splitString = vector3String.Replace(" ", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty).Split(',');

            if (splitString.Length < 3) return false;
            
            vector3 = new(float.Parse(splitString[0], CultureInfo.InvariantCulture), float.Parse(splitString[1], CultureInfo.InvariantCulture), float.Parse(splitString[2], CultureInfo.InvariantCulture));
            
            return true;
        }

        public static string ToName(this Sounds value)
        {
            return value switch
            {
                Sounds.None => "None",
                Sounds.Sound01 => "Sound Effect 1",
                Sounds.Sound02 => "Sound Effect 2",
                Sounds.Sound03 => "Sound Effect 3",
                Sounds.Sound04 => "Sound Effect 4",
                Sounds.Sound05 => "Sound Effect 5",
                Sounds.Sound06 => "Sound Effect 6",
                Sounds.Sound07 => "Sound Effect 7",
                Sounds.Sound08 => "Sound Effect 8",
                Sounds.Sound09 => "Sound Effect 9",
                Sounds.Sound10 => "Sound Effect 10",
                Sounds.Sound11 => "Sound Effect 11",
                Sounds.Sound12 => "Sound Effect 12",
                Sounds.Sound13 => "Sound Effect 13",
                Sounds.Sound14 => "Sound Effect 14",
                Sounds.Sound15 => "Sound Effect 15",
                Sounds.Sound16 => "Sound Effect 16",
                _ => "Unknown",
            };
        }
    }
}
