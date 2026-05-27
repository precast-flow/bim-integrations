using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BimPrefabExport.UI;

/// <summary>Tipoloji şeması satırı (etiket + değer), palet ve ürün düzenleyicide ortak.</summary>
public sealed class AttributeValueRow : INotifyPropertyChanged
{
    public AttributeValueRow(string tag, string label, string fieldType)
    {
        Tag = tag;
        Label = label;
        FieldType = fieldType;
    }

    public string Tag { get; }
    public string Label { get; }
    public string FieldType { get; }

    private string _value = "";

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
                return;
            _value = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
