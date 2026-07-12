namespace ModernFormsNext
{
    /// <summary>
    /// Defines the text format used by <see cref="RichTextBox.LoadFile(string, RichTextBoxStreamType)"/>
    /// and <see cref="RichTextBox.SaveFile(string, RichTextBoxStreamType)"/>.
    /// </summary>
    /// <remarks>
    /// ModernFormsNext supports plain text and a portable subset of RTF. OLE object stream
    /// variants are accepted for source compatibility but are saved and loaded without OLE data.
    /// </remarks>
    public enum RichTextBoxStreamType
    {
        /// <summary>
        /// Rich Text Format.
        /// </summary>
        RichText = 0,

        /// <summary>
        /// Plain text.
        /// </summary>
        PlainText = 1,

        /// <summary>
        /// Rich Text Format without OLE objects.
        /// </summary>
        RichNoOleObjs = 2,

        /// <summary>
        /// Plain text with textual placeholders for OLE objects.
        /// </summary>
        TextTextOleObjs = 3,

        /// <summary>
        /// Plain text encoded as UTF-16.
        /// </summary>
        UnicodePlainText = 4,
    }
}
