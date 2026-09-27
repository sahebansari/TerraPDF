namespace TerraPDF.Benchmarks.Scenarios;

/// <summary>Deterministic filler text shared by the scenarios.</summary>
internal static class Text
{
    internal const string Paragraph =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut "
      + "labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris "
      + "nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit "
      + "esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt "
      + "in culpa qui officia deserunt mollit anim id est laborum.";

    internal const string Multilingual =
        "TerraPDF теперь встраивает настоящие TrueType-шрифты. Το TerraPDF ενσωματώνει πλέον πραγματικές "
      + "γραμματοσειρές TrueType. café, Wörld, naïve, Zürich — “quotes” and € signs.";

    internal const string Devanagari =
        "धर्म, प्रधानमंत्री, स्वास्थ्य, राष्ट्रीय, कार्यक्रम। भारत में बच्चों के पोषण की स्थिति पर यह रिपोर्ट "
      + "राष्ट्रीय परिवार स्वास्थ्य सर्वेक्षण के आंकड़ों पर आधारित है।";

    /// <summary>Roughly how many <see cref="Paragraph"/>s fill one A4 page at 11pt with 2cm margins.</summary>
    internal const int ParagraphsPerPage = 8;
}
