namespace SkiaQualification;

// Keep IDs and strings unchanged when comparing the two Skia versions.
internal static class TextCorpus
{
    internal const string Version = "multilingual-text-v1";
    internal static readonly (string Id, string Text)[] Cases =
    [
        ("latin-kerning", "AVATAR To Wa office ffi"),
        ("polish", "Zażółć gęślą jaźń. Pchnąć w tę łódź jeża."),
        ("cyrillic", "Съешь ещё этих мягких булок."),
        ("greek", "Ταχίστη αλώπηξ βαφής ψημένη."),
        ("cjk-fallback", "中文 測試 · 日本語 かな · 한국어 한글"),
        ("arabic", "السَّلَامُ عَلَيْكُمْ لا"),
        ("hebrew", "שָׁלוֹם עוֹלָם"),
        ("devanagari", "नमस्ते दुनिया क्ष त्र ज्ञ"),
        ("bengali", "বাংলা লেখা ক্ষ"),
        ("mixed-bidi", "ABC العربية 123 שלום XYZ"),
        ("emoji-presentation", "✈︎ ✈️ ☀︎ ☀️ 😀"),
        ("emoji-zwj", "👩🏽‍💻 👨‍👩‍👧‍👦 🏳️‍🌈"),
        ("combining", "Cafe\u0301 A\u030A a\u0301\u0323"),
        ("supplementary", "𐐷 𝄞 𠀀"),
        ("multiline", "Zażółć office ffi\n日本語 العربية שלום\n👩🏽‍💻 A\u030A")
    ];
}
