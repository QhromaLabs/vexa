using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Vexa.Converters;

public static class RichTextBoxHelper
{
    public static readonly DependencyProperty BoundTextProperty =
        DependencyProperty.RegisterAttached("BoundText", typeof(string), typeof(RichTextBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundTextChanged));

    public static string GetBoundText(DependencyObject obj) => (string)obj.GetValue(BoundTextProperty);

    public static void SetBoundText(DependencyObject obj, string value) => obj.SetValue(BoundTextProperty, value);

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox richTextBox)
        {
            return;
        }

        var newText = e.NewValue as string ?? string.Empty;
        var existingText = GetDocumentText(richTextBox);
        if (string.Equals(existingText, newText, StringComparison.Ordinal))
        {
            return;
        }

        richTextBox.TextChanged -= OnRichTextBoxTextChanged;
        SetDocumentText(richTextBox, newText);
        richTextBox.TextChanged += OnRichTextBoxTextChanged;
    }

    private static void OnRichTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not RichTextBox richTextBox)
        {
            return;
        }

        var text = GetDocumentText(richTextBox);
        SetBoundText(richTextBox, text);
    }

    private static void SetDocumentText(RichTextBox richTextBox, string text)
    {
        var document = richTextBox.Document ?? new FlowDocument();
        document.Blocks.Clear();
        document.Blocks.Add(new Paragraph(new Run(text)));
        richTextBox.Document = document;
    }

    private static string GetDocumentText(RichTextBox richTextBox)
    {
        var textRange = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
        var text = textRange.Text ?? string.Empty;
        return text.TrimEnd('\r', '\n');
    }

    public static void InsertAtCaret(RichTextBox richTextBox, string text)
    {
        var caret = richTextBox.CaretPosition;
        caret.InsertTextInRun(text);
        richTextBox.CaretPosition = caret;
    }
}
