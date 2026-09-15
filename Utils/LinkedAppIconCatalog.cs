using System.Windows.Media;

namespace SharpShot.Utils
{
    public static class LinkedAppIconCatalog
    {
        public static readonly string[] IconKeys =
        {
            "Window", "Camera", "Video", "Region", "Settings", "OBS", "FullScreen", "Star", "Play", "Letters"
        };

        public static readonly string[] MenuKeys = { "Main", "Recording", "Both" };

        public const string WindowData = "M2,4 L22,4 L22,20 L2,20 Z M2,8 L22,8 M6,6 L6,6.1 M9,6 L9,6.1";
        public const string StarData = "M12,2 L14.5,8.5 L21.5,9 L16,13.5 L17.8,20.5 L12,16.8 L6.2,20.5 L8,13.5 L2.5,9 L9.5,8.5 Z";
        public const string PlayData = "M6,4 L20,12 L6,20 Z";

        public static Geometry WindowGeometry => Geometry.Parse(WindowData);
        public static Geometry StarGeometry => Geometry.Parse(StarData);
        public static Geometry PlayGeometry => Geometry.Parse(PlayData);

        public static string DisplayName(string icon) => icon switch
        {
            "Window" => "Window",
            "Camera" => "Camera",
            "Video" => "Video",
            "Region" => "Region",
            "Settings" => "Gear",
            "OBS" => "OBS",
            "FullScreen" => "Fullscreen",
            "Star" => "Star",
            "Play" => "Play",
            "Letters" => "3 letters",
            _ => "Window"
        };

        public static string MenuDisplayName(string menu) => menu switch
        {
            "Recording" => "Recording menu",
            "Both" => "Main + Recording",
            _ => "Main dashboard"
        };
    }
}
