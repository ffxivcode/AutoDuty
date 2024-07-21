using System.Text.RegularExpressions;

namespace AutoDuty.Helpers
{
    public static partial class RegexHelper
    {
        [GeneratedRegex(@"([^<]*)?(?><?([0-9\. ]*\,[0-9\. ]*\,[0-9\. ]*)>([^<]*)<\/>)?", RegexOptions.CultureInvariant)]
        public static partial Regex ColoredTextRegex();

        [GeneratedRegex($@"(\()([0-9]{{3,4}})(\))( {PathIdentifiers.W2W})?(.*)(\.json)", RegexOptions.CultureInvariant)]
        public static partial Regex PathFileRegex();

        [GeneratedRegex(@"([0-9]{3,})", RegexOptions.CultureInvariant)]
        public static partial Regex InteractionObjectIdRegex();
    }

    public static class PathIdentifiers
    {
        public const string W2W = @"「W2W-まとめ」";
    }
}
