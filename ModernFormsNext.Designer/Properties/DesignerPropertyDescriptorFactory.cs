using ModernFormsNext.Designing;
using SkiaSharp;

namespace ModernFormsNext.Designer.Properties;

internal static class DesignerPropertyDescriptorFactory
{
    private const int MinimumControlSize = 8;

    public static DesignerPropertyDescriptor CreateDocumentSize(DesignDocument document)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = "Size",
            Path = "Size",
            DisplayName = "Size",
            Category = "Layout",
            Description = "The form client size in logical pixels.",
            ValueType = typeof(DesignSize),
            IsAdvanced = true,
            GetValue = () => document.Size,
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(DesignSize), out var value, out var error))
                    return (false, error);

                var size = (DesignSize)value!;

                if (size.Width < 1 || size.Height < 1)
                    return (false, "The form size must be greater than zero.");

                document.Size = size;
                return (true, null);
            }
        };

        root.Children.Add(CreateIntChild(
            "Width",
            "Size.Width",
            "Width",
            "Layout",
            "The form width in logical pixels.",
            () => document.Size.Width,
            value =>
            {
                if (value < 1)
                    return (false, "The form width must be greater than zero.");

                document.Size = new DesignSize(value, document.Size.Height);
                return (true, null);
            },
            depth: 1));

        root.Children.Add(CreateIntChild(
            "Height",
            "Size.Height",
            "Height",
            "Layout",
            "The form height in logical pixels.",
            () => document.Size.Height,
            value =>
            {
                if (value < 1)
                    return (false, "The form height must be greater than zero.");

                document.Size = new DesignSize(document.Size.Width, value);
                return (true, null);
            },
            depth: 1));

        return root;
    }

    public static DesignerPropertyDescriptor CreateNodeBounds(DesignControlNode node)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = "Bounds",
            Path = "Bounds",
            DisplayName = "Bounds",
            Category = "Layout",
            Description = "The control bounds relative to its parent container.",
            ValueType = typeof(DesignBounds),
            IsAdvanced = true,
            GetValue = () => node.Bounds,
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(DesignBounds), out var value, out var error))
                    return (false, error);

                var bounds = (DesignBounds)value!;

                if (bounds.Width < MinimumControlSize || bounds.Height < MinimumControlSize)
                    return (false, $"Bounds width and height must be at least {MinimumControlSize}.");

                node.Bounds = bounds;
                return (true, null);
            }
        };

        root.Children.Add(CreateNodeBoundsChild(node, "X", "Bounds.X", "X", "Horizontal position relative to the parent container.", bounds => bounds.X, (bounds, value) => new DesignBounds(value, bounds.Y, bounds.Width, bounds.Height), false, 1));
        root.Children.Add(CreateNodeBoundsChild(node, "Y", "Bounds.Y", "Y", "Vertical position relative to the parent container.", bounds => bounds.Y, (bounds, value) => new DesignBounds(bounds.X, value, bounds.Width, bounds.Height), false, 1));
        root.Children.Add(CreateNodeBoundsChild(node, "Width", "Bounds.Width", "Width", "Width in logical pixels.", bounds => bounds.Width, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, value, bounds.Height), true, 1));
        root.Children.Add(CreateNodeBoundsChild(node, "Height", "Bounds.Height", "Height", "Height in logical pixels.", bounds => bounds.Height, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, bounds.Width, value), true, 1));

        return root;
    }

    public static DesignerPropertyDescriptor CreateNodeLocation(DesignControlNode node)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = "Location",
            Path = "Location",
            DisplayName = "Location",
            Category = "Layout",
            Description = "The control location relative to its parent container.",
            ValueType = typeof(DesignPoint),
            IsAdvanced = true,
            GetValue = () => new DesignPoint(node.Bounds.X, node.Bounds.Y),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(DesignPoint), out var value, out var error))
                    return (false, error);

                var point = (DesignPoint)value!;
                node.Bounds = new DesignBounds(point.X, point.Y, node.Bounds.Width, node.Bounds.Height);
                return (true, null);
            }
        };

        root.Children.Add(CreateNodeBoundsChild(node, "X", "Location.X", "X", "Horizontal position relative to the parent container.", bounds => bounds.X, (bounds, value) => new DesignBounds(value, bounds.Y, bounds.Width, bounds.Height), false, 1));
        root.Children.Add(CreateNodeBoundsChild(node, "Y", "Location.Y", "Y", "Vertical position relative to the parent container.", bounds => bounds.Y, (bounds, value) => new DesignBounds(bounds.X, value, bounds.Width, bounds.Height), false, 1));

        return root;
    }

    public static DesignerPropertyDescriptor CreateNodeSize(DesignControlNode node)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = "Size",
            Path = "Size",
            DisplayName = "Size",
            Category = "Layout",
            Description = "The control size in logical pixels.",
            ValueType = typeof(DesignSize),
            IsAdvanced = true,
            GetValue = () => new DesignSize(node.Bounds.Width, node.Bounds.Height),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(DesignSize), out var value, out var error))
                    return (false, error);

                var size = (DesignSize)value!;

                if (size.Width < MinimumControlSize || size.Height < MinimumControlSize)
                    return (false, $"Size values must be at least {MinimumControlSize}.");

                node.Bounds = new DesignBounds(node.Bounds.X, node.Bounds.Y, size.Width, size.Height);
                return (true, null);
            }
        };

        root.Children.Add(CreateNodeBoundsChild(node, "Width", "Size.Width", "Width", "Width in logical pixels.", bounds => bounds.Width, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, value, bounds.Height), true, 1));
        root.Children.Add(CreateNodeBoundsChild(node, "Height", "Size.Height", "Height", "Height in logical pixels.", bounds => bounds.Height, (bounds, value) => new DesignBounds(bounds.X, bounds.Y, bounds.Width, value), true, 1));

        return root;
    }

    public static bool TryCreateRuntimeDescriptor(
        DesignControlNode node,
        string name,
        string displayName,
        string category,
        string description,
        Type propertyType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced,
        out DesignerPropertyDescriptor? descriptor)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        descriptor = null;

        if (type == typeof(ControlStyle))
        {
            descriptor = CreateStyleDescriptor(node, name, displayName, category, description, fallbackValue as ControlStyle, isAdvanced);
            return true;
        }

        if (type == typeof(System.Drawing.Rectangle))
        {
            descriptor = CreateStoredRectangleDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(System.Drawing.Size))
        {
            descriptor = CreateStoredSizeDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(System.Drawing.Point))
        {
            descriptor = CreateStoredPointDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(System.Drawing.PointF))
        {
            descriptor = CreateStoredPointFDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(Padding))
        {
            descriptor = CreateStoredPaddingDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(SKPoint))
        {
            descriptor = CreateStoredSkPointDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(SKSize))
        {
            descriptor = CreateStoredSkSizeDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(SKRect))
        {
            descriptor = CreateStoredSkRectDescriptor(node, name, displayName, category, description, propertyType, fallbackValue, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(SKColor))
        {
            descriptor = CreateStoredColorDescriptor(node, name, displayName, category, description, propertyType, fallbackValue as SKColor?, canEdit, 0, isAdvanced);
            return true;
        }

        if (type == typeof(Font))
        {
            descriptor = CreateStoredFontDescriptor(node, name, displayName, category, description, propertyType, fallbackValue as Font, canEdit, 0, isAdvanced);
            return true;
        }

        if (type == typeof(SKBitmap) && string.Equals(name, "Image", StringComparison.Ordinal))
        {
            descriptor = CreateStoredImageDescriptor(node, displayName, category, description, canEdit, isAdvanced);
            return true;
        }

        if (typeof(ModernFormsNext.Drawing.Brush).IsAssignableFrom(type))
        {
            descriptor = CreateStoredBrushDescriptor(node, name, displayName, category, description, propertyType, fallbackValue as ModernFormsNext.Drawing.Brush, canEdit, isAdvanced);
            return true;
        }

        if (type == typeof(ModernFormsNext.Drawing.PointCollection))
        {
            Func<ModernFormsNext.Drawing.PointCollection?> getPoints = () =>
                GetStoredValue(node, name, propertyType, fallbackValue) as ModernFormsNext.Drawing.PointCollection;
            descriptor = CreateStoredRoot(
                node,
                name,
                displayName,
                category,
                description,
                propertyType,
                fallbackValue,
                canEdit,
                isAdvanced,
                DesignerPropertyDialogEditors.PointCollection(
                    getPoints,
                    points =>
                    {
                        SetStoredValue(node, name, points, propertyType);
                        return (true, null);
                    }));
            return true;
        }

        if (typeof(ModernFormsNext.Drawing.Geometry).IsAssignableFrom(type))
        {
            Func<ModernFormsNext.Drawing.Geometry?> getGeometry = () =>
                GetStoredValue(node, name, propertyType, fallbackValue) as ModernFormsNext.Drawing.Geometry;
            descriptor = CreateStoredRoot(
                node,
                name,
                displayName,
                category,
                description,
                propertyType,
                fallbackValue,
                canEdit,
                isAdvanced,
                DesignerPropertyDialogEditors.PathGeometry(
                    getGeometry,
                    geometry =>
                    {
                        SetStoredValue(node, name, geometry, propertyType);
                        return (true, null);
                    }));
            return true;
        }

        return false;
    }

    private static DesignerPropertyDescriptor CreateNodeBoundsChild(
        DesignControlNode node,
        string name,
        string path,
        string displayName,
        string description,
        Func<DesignBounds, int> getValue,
        Func<DesignBounds, int, DesignBounds> createBounds,
        bool requireMinimum,
        int depth)
        => CreateIntChild(
            name,
            path,
            displayName,
            "Layout",
            description,
            () => getValue(node.Bounds),
            value =>
            {
                if (requireMinimum && value < MinimumControlSize)
                    return (false, $"The value must be at least {MinimumControlSize}.");

                node.Bounds = createBounds(node.Bounds, value);
                return (true, null);
            },
            depth);

    private static DesignerPropertyDescriptor CreateStoredRectangleDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredRectangleChild(node, path, "X", "X", "Horizontal position.", valueType, fallbackValue, rectangle => rectangle.X, (rectangle, value) => new System.Drawing.Rectangle(value, rectangle.Y, rectangle.Width, rectangle.Height), false, 1, canEdit, category));
        root.Children.Add(CreateStoredRectangleChild(node, path, "Y", "Y", "Vertical position.", valueType, fallbackValue, rectangle => rectangle.Y, (rectangle, value) => new System.Drawing.Rectangle(rectangle.X, value, rectangle.Width, rectangle.Height), false, 1, canEdit, category));
        root.Children.Add(CreateStoredRectangleChild(node, path, "Width", "Width", "Width in logical pixels.", valueType, fallbackValue, rectangle => rectangle.Width, (rectangle, value) => new System.Drawing.Rectangle(rectangle.X, rectangle.Y, value, rectangle.Height), true, 1, canEdit, category));
        root.Children.Add(CreateStoredRectangleChild(node, path, "Height", "Height", "Height in logical pixels.", valueType, fallbackValue, rectangle => rectangle.Height, (rectangle, value) => new System.Drawing.Rectangle(rectangle.X, rectangle.Y, rectangle.Width, value), true, 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredSizeDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredSizeChild(node, path, "Width", "Width", "Width in logical pixels.", valueType, fallbackValue, size => size.Width, (size, value) => new System.Drawing.Size(value, size.Height), 1, canEdit, category));
        root.Children.Add(CreateStoredSizeChild(node, path, "Height", "Height", "Height in logical pixels.", valueType, fallbackValue, size => size.Height, (size, value) => new System.Drawing.Size(size.Width, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredPointDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredPointChild(node, path, "X", "X", "Horizontal position.", valueType, fallbackValue, point => point.X, (point, value) => new System.Drawing.Point(value, point.Y), 1, canEdit, category));
        root.Children.Add(CreateStoredPointChild(node, path, "Y", "Y", "Vertical position.", valueType, fallbackValue, point => point.Y, (point, value) => new System.Drawing.Point(point.X, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredPointFDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredPointFChild(node, path, "X", "X", "Horizontal position in logical pixels.", valueType, fallbackValue, point => point.X, (point, value) => new System.Drawing.PointF(value, point.Y), 1, canEdit, category));
        root.Children.Add(CreateStoredPointFChild(node, path, "Y", "Y", "Vertical position in logical pixels.", valueType, fallbackValue, point => point.Y, (point, value) => new System.Drawing.PointF(point.X, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredPaddingDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredPaddingChild(node, path, "Left", "Left", "Left edge spacing.", valueType, fallbackValue, padding => padding.Left, (padding, value) => new Padding(value, padding.Top, padding.Right, padding.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredPaddingChild(node, path, "Top", "Top", "Top edge spacing.", valueType, fallbackValue, padding => padding.Top, (padding, value) => new Padding(padding.Left, value, padding.Right, padding.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredPaddingChild(node, path, "Right", "Right", "Right edge spacing.", valueType, fallbackValue, padding => padding.Right, (padding, value) => new Padding(padding.Left, padding.Top, value, padding.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredPaddingChild(node, path, "Bottom", "Bottom", "Bottom edge spacing.", valueType, fallbackValue, padding => padding.Bottom, (padding, value) => new Padding(padding.Left, padding.Top, padding.Right, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredSkPointDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredSkPointChild(node, path, "X", "X", "The horizontal component.", valueType, fallbackValue, point => point.X, (point, value) => new SKPoint(value, point.Y), 1, canEdit, category));
        root.Children.Add(CreateStoredSkPointChild(node, path, "Y", "Y", "The vertical component.", valueType, fallbackValue, point => point.Y, (point, value) => new SKPoint(point.X, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredSkSizeDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredSkSizeChild(node, path, "Width", "Width", "The width component.", valueType, fallbackValue, size => size.Width, (size, value) => new SKSize(value, size.Height), 1, canEdit, category));
        root.Children.Add(CreateStoredSkSizeChild(node, path, "Height", "Height", "The height component.", valueType, fallbackValue, size => size.Height, (size, value) => new SKSize(size.Width, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredSkRectDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced)
    {
        var root = CreateStoredRoot(node, path, displayName, category, description, valueType, fallbackValue, canEdit, isAdvanced);
        root.Children.Add(CreateStoredSkRectChild(node, path, "Left", "Left", "The left edge.", valueType, fallbackValue, rect => rect.Left, (rect, value) => new SKRect(value, rect.Top, rect.Right, rect.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredSkRectChild(node, path, "Top", "Top", "The top edge.", valueType, fallbackValue, rect => rect.Top, (rect, value) => new SKRect(rect.Left, value, rect.Right, rect.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredSkRectChild(node, path, "Right", "Right", "The right edge.", valueType, fallbackValue, rect => rect.Right, (rect, value) => new SKRect(rect.Left, rect.Top, value, rect.Bottom), 1, canEdit, category));
        root.Children.Add(CreateStoredSkRectChild(node, path, "Bottom", "Bottom", "The bottom edge.", valueType, fallbackValue, rect => rect.Bottom, (rect, value) => new SKRect(rect.Left, rect.Top, rect.Right, value), 1, canEdit, category));
        return root;
    }

    private static DesignerPropertyDescriptor CreateStyleDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        ControlStyle? fallbackStyle,
        bool isAdvanced)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(ControlStyle),
            IsReadOnly = true,
            IsAdvanced = true,
            ShouldSerialize = false,
            GetValue = () => "Expandable style"
        };

        root.Children.Add(CreateStoredColorDescriptor(node, $"{path}.BackgroundColor", "BackgroundColor", category, "The explicit style background color.", typeof(SKColor?), fallbackStyle?.BackgroundColor, canEdit: true, depth: 1, isAdvanced));
        root.Children.Add(CreateStoredColorDescriptor(node, $"{path}.ForegroundColor", "ForegroundColor", category, "The explicit style foreground color.", typeof(SKColor?), fallbackStyle?.ForegroundColor, canEdit: true, depth: 1, isAdvanced));
        root.Children.Add(CreateStoredNullableIntDescriptor(node, $"{path}.FontSize", "FontSize", category, "The explicit style font size in points.", fallbackStyle?.FontSize, canEdit: true, depth: 1, isAdvanced, requirePositive: true));
        root.Children.Add(CreateStoredFontDescriptor(node, $"{path}.TextFont", "TextFont", category, "The explicit ModernFormsNext font for this style.", typeof(Font), fallbackStyle?.TextFont, canEdit: true, depth: 1, isAdvanced));
        root.Children.Add(CreateBorderDescriptor(node, $"{path}.Border", "Border", category, "The explicit border style values.", fallbackStyle?.Border, depth: 1, isAdvanced));

        return root;
    }

    private static DesignerPropertyDescriptor CreateBorderDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        BorderStyle? fallbackBorder,
        int depth,
        bool isAdvanced)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(BorderStyle),
            IsReadOnly = true,
            IsAdvanced = isAdvanced,
            ShouldSerialize = false,
            Depth = depth,
            GetValue = () => "Expandable border"
        };

        root.Children.Add(CreateStoredColorDescriptor(node, $"{path}.Color", "Color", category, "The color applied to all border sides.", typeof(SKColor?), fallbackBorder?.Color, canEdit: true, depth + 1, isAdvanced));
        root.Children.Add(CreateStoredNullableIntDescriptor(node, $"{path}.Width", "Width", category, "The width applied to all border sides.", fallbackBorder?.Width, canEdit: true, depth + 1, isAdvanced, requirePositive: false));
        root.Children.Add(CreateStoredNullableIntDescriptor(node, $"{path}.Radius", "Radius", category, "The radius applied to all border corners.", fallbackBorder?.Radius, canEdit: true, depth + 1, isAdvanced, requirePositive: false));
        root.Children.Add(CreateBorderSideDescriptor(node, $"{path}.Left", "Left", category, "The left border side.", fallbackBorder?.Left, depth + 1, isAdvanced));
        root.Children.Add(CreateBorderSideDescriptor(node, $"{path}.Top", "Top", category, "The top border side.", fallbackBorder?.Top, depth + 1, isAdvanced));
        root.Children.Add(CreateBorderSideDescriptor(node, $"{path}.Right", "Right", category, "The right border side.", fallbackBorder?.Right, depth + 1, isAdvanced));
        root.Children.Add(CreateBorderSideDescriptor(node, $"{path}.Bottom", "Bottom", category, "The bottom border side.", fallbackBorder?.Bottom, depth + 1, isAdvanced));

        return root;
    }

    private static DesignerPropertyDescriptor CreateBorderSideDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        BorderSideStyle? fallbackSide,
        int depth,
        bool isAdvanced)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(BorderSideStyle),
            IsReadOnly = true,
            IsAdvanced = isAdvanced,
            ShouldSerialize = false,
            Depth = depth,
            GetValue = () => "Expandable side"
        };

        root.Children.Add(CreateStoredColorDescriptor(node, $"{path}.Color", "Color", category, "The color of this border side.", typeof(SKColor?), fallbackSide?.Color, canEdit: true, depth + 1, isAdvanced));
        root.Children.Add(CreateStoredNullableIntDescriptor(node, $"{path}.Width", "Width", category, "The width of this border side.", fallbackSide?.Width, canEdit: true, depth + 1, isAdvanced, requirePositive: false));

        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredRoot(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        object? fallbackValue,
        bool canEdit,
        bool isAdvanced,
        Func<DesignerPropertyDialogContext, Task<bool>>? dialogEditor = null)
        => new()
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            IsReadOnly = !canEdit,
            IsAdvanced = isAdvanced,
            ShouldSerialize = canEdit,
            HasDialogEditor = canEdit && dialogEditor is not null,
            DialogEditor = canEdit ? dialogEditor : null,
            GetValue = () => GetStoredValue(node, path, valueType, fallbackValue),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This property is read-only.");

                if (!DesignerPropertyValueEditor.TryConvert(text, valueType, out var value, out var error))
                    return (false, error);

                SetStoredValue(node, path, value, valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateStoredColorDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        SKColor? fallbackColor,
        bool canEdit,
        int depth,
        bool isAdvanced)
    {
        var root = CreateColorDescriptor(
            path,
            displayName,
            category,
            description,
            valueType,
            () => GetStoredColor(node, path, valueType, fallbackColor),
            color =>
            {
                SetStoredValue(node, path, color, valueType);
                return (true, null);
            },
            canEdit,
            depth,
            isAdvanced,
            nullable: Nullable.GetUnderlyingType(valueType) is not null);

        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredBrushDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        bool canEdit,
        bool isAdvanced)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            IsReadOnly = !canEdit,
            IsAdvanced = true,
            HasDialogEditor = canEdit,
            DialogEditor = canEdit ? DesignerPropertyDialogEditors.Brush(
                () => GetStoredBrush(node, path, valueType, fallbackBrush),
                brush =>
                {
                    SetStoredValue(node, path, brush, valueType);
                    return (true, null);
                }) : null,
            GetValue = () => GetStoredValue(node, path, valueType, fallbackBrush),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This brush is read-only.");

                if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                {
                    node.Properties[path] = DesignPropertyValue.FromNull();
                    return (true, null);
                }

                if (!DesignerPropertyValueEditor.TryConvert(text, valueType, out var value, out var error))
                    return (false, error);

                SetStoredValue(node, path, value, valueType);
                return (true, null);
            }
        };

        root.Children.Add(CreateBrushTypeDescriptor(node, path, valueType, fallbackBrush, category));

        var brush = GetStoredBrush(node, path, valueType, fallbackBrush);

        if (brush is null or ModernFormsNext.Drawing.SolidColorBrush)
        {
            root.Children.Add(CreateColorDescriptor(
                $"{path}.Color",
                "Color",
                category,
                "The solid color used by this brush. Setting a color creates a SolidColorBrush.",
                typeof(SKColor),
                () => GetBrushColor(GetStoredBrush(node, path, valueType, fallbackBrush)),
                color =>
                {
                    if (color is null)
                        node.Properties[path] = DesignPropertyValue.FromNull();
                    else
                        SetStoredValue(
                            node,
                            path,
                            CreateSolidBrushPreservingCommonProperties(
                                GetStoredBrush(node, path, valueType, fallbackBrush),
                                color.Value),
                            valueType);

                    return (true, null);
                },
                canEdit,
                depth: 1,
                isAdvanced,
                nullable: true));
        }
        else if (brush is ModernFormsNext.Drawing.GlassBrush)
        {
            root.Children.Add(CreateGlassBrushColorDescriptor(node, path, "TintColor", "TintColor", "The main tint color of the glass brush.", valueType, fallbackBrush, glass => glass.TintColor, (glass, color) => glass.TintColor = color, canEdit, 1, category, isAdvanced));
            root.Children.Add(CreateGlassBrushColorDescriptor(node, path, "SecondaryTintColor", "SecondaryTintColor", "The secondary tint color used by the glass brush.", valueType, fallbackBrush, glass => glass.SecondaryTintColor, (glass, color) => glass.SecondaryTintColor = color, canEdit, 1, category, isAdvanced));
            root.Children.Add(CreateGlassBrushColorDescriptor(node, path, "HighlightColor", "HighlightColor", "The soft highlight color used by the glass brush.", valueType, fallbackBrush, glass => glass.HighlightColor, (glass, color) => glass.HighlightColor = color, canEdit, 1, category, isAdvanced));
            root.Children.Add(CreateGlassBrushColorDescriptor(node, path, "BorderColor", "BorderColor", "The border color used by the glass brush.", valueType, fallbackBrush, glass => glass.BorderColor, (glass, color) => glass.BorderColor = color, canEdit, 1, category, isAdvanced));
            root.Children.Add(CreateGlassBrushBooleanDescriptor(node, path, "ShowHighlight", "ShowHighlight", "Whether the glass brush draws the soft top highlight.", valueType, fallbackBrush, glass => glass.ShowHighlight, (glass, value) => glass.ShowHighlight = value, canEdit, 1, category));
            root.Children.Add(CreateGlassBrushBooleanDescriptor(node, path, "ShowInnerBorder", "ShowInnerBorder", "Whether the glass brush draws the inner border.", valueType, fallbackBrush, glass => glass.ShowInnerBorder, (glass, value) => glass.ShowInnerBorder = value, canEdit, 1, category));
        }
        else if (brush is ModernFormsNext.Drawing.LinearGradientBrush)
        {
            root.Children.Add(CreateLinearGradientPointDescriptor(node, path, "StartPoint", "StartPoint", "The normalized start point of the linear gradient.", valueType, fallbackBrush, gradient => gradient.StartPoint, (gradient, value) => gradient.StartPoint = value, canEdit, 1, category));
            root.Children.Add(CreateLinearGradientPointDescriptor(node, path, "EndPoint", "EndPoint", "The normalized end point of the linear gradient.", valueType, fallbackBrush, gradient => gradient.EndPoint, (gradient, value) => gradient.EndPoint = value, canEdit, 1, category));
            root.Children.Add(CreateGradientStopCountDescriptor(node, path, valueType, fallbackBrush, category, depth: 1));
        }
        else if (brush is ModernFormsNext.Drawing.RadialGradientBrush)
        {
            root.Children.Add(CreateRadialGradientPointDescriptor(node, path, "Center", "Center", "The normalized center point of the radial gradient.", valueType, fallbackBrush, gradient => gradient.Center, (gradient, value) => gradient.Center = value, canEdit, 1, category));
            root.Children.Add(CreateRadialGradientFloatDescriptor(node, path, "Radius", "Radius", "The normalized radius of the radial gradient.", valueType, fallbackBrush, gradient => gradient.Radius, (gradient, value) => gradient.Radius = value, value => value >= 0f, "The radius cannot be negative.", canEdit, 1, category));
            root.Children.Add(CreateGradientStopCountDescriptor(node, path, valueType, fallbackBrush, category, depth: 1));
        }
        else if (brush is ModernFormsNext.Drawing.SweepGradientBrush)
        {
            root.Children.Add(CreateSweepGradientPointDescriptor(node, path, "Center", "Center", "The normalized center point of the sweep gradient.", valueType, fallbackBrush, gradient => gradient.Center, (gradient, value) => gradient.Center = value, canEdit, 1, category));
            root.Children.Add(CreateSweepGradientFloatDescriptor(node, path, "StartAngle", "StartAngle", "The start angle of the sweep gradient in degrees.", valueType, fallbackBrush, gradient => gradient.StartAngle, (gradient, value) => gradient.StartAngle = value, _ => true, null, canEdit, 1, category));
            root.Children.Add(CreateSweepGradientFloatDescriptor(node, path, "EndAngle", "EndAngle", "The end angle of the sweep gradient in degrees.", valueType, fallbackBrush, gradient => gradient.EndAngle, (gradient, value) => gradient.EndAngle = value, _ => true, null, canEdit, 1, category));
            root.Children.Add(CreateGradientStopCountDescriptor(node, path, valueType, fallbackBrush, category, depth: 1));
        }
        else
        {
            root.Children.Add(new DesignerPropertyDescriptor
            {
                Name = "UnsupportedBrush",
                Path = $"{path}.UnsupportedBrush",
                DisplayName = "UnsupportedBrush",
                Category = category,
                Description = "This brush subtype is not editable by ModernFormsNext Designer yet.",
                ValueType = typeof(string),
                IsReadOnly = true,
                IsAdvanced = true,
                Depth = 1,
                GetValue = () => brush.GetType().Name
            });
        }

        return root;
    }

    private static DesignerPropertyDescriptor CreateBrushTypeDescriptor(
        DesignControlNode node,
        string path,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        string category)
        => new()
        {
            Name = "Type",
            Path = $"{path}.Type",
            DisplayName = "Type",
            Category = category,
            Description = "The concrete brush type currently assigned to this property.",
            ValueType = typeof(string),
            IsReadOnly = true,
            Depth = 1,
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush)?.GetType().Name ?? "null"
        };

    private static DesignerPropertyDescriptor CreateColorDescriptor(
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        Func<SKColor?> getColor,
        Func<SKColor?, (bool Success, string? Error)> setColor,
        bool canEdit,
        int depth,
        bool isAdvanced,
        bool nullable)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            IsReadOnly = !canEdit,
            IsAdvanced = isAdvanced,
            HasDialogEditor = canEdit,
            DialogEditor = canEdit ? DesignerPropertyDialogEditors.Color(getColor, setColor) : null,
            Depth = depth,
            GetValue = () => getColor(),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This color is read-only.");

                if (nullable && (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase)))
                    return setColor(null);

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKColor), out var value, out var error))
                    return (false, error);

                return setColor((SKColor)value!);
            }
        };

        root.Children.Add(new DesignerPropertyDescriptor
        {
            Name = "Hex",
            Path = $"{path}.Hex",
            DisplayName = "Hex",
            Category = category,
            Description = "The color encoded as #RRGGBB or #AARRGGBB.",
            ValueType = typeof(string),
            IsReadOnly = !canEdit,
            Depth = depth + 1,
            GetValue = () => getColor() is { } color ? DesignerPropertyValueEditor.ToHex(color) : string.Empty,
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKColor), out var value, out var error))
                    return (false, error);

                return setColor((SKColor)value!);
            }
        });

        root.Children.Add(CreateColorComponentChild(path, "A", "Alpha channel from 0 to 255.", getColor, setColor, color => color.Alpha, (color, value) => new SKColor(color.Red, color.Green, color.Blue, (byte)value), canEdit, depth + 1, category));
        root.Children.Add(CreateColorComponentChild(path, "R", "Red channel from 0 to 255.", getColor, setColor, color => color.Red, (color, value) => new SKColor((byte)value, color.Green, color.Blue, color.Alpha), canEdit, depth + 1, category));
        root.Children.Add(CreateColorComponentChild(path, "G", "Green channel from 0 to 255.", getColor, setColor, color => color.Green, (color, value) => new SKColor(color.Red, (byte)value, color.Blue, color.Alpha), canEdit, depth + 1, category));
        root.Children.Add(CreateColorComponentChild(path, "B", "Blue channel from 0 to 255.", getColor, setColor, color => color.Blue, (color, value) => new SKColor(color.Red, color.Green, (byte)value, color.Alpha), canEdit, depth + 1, category));

        return root;
    }

    private static DesignerPropertyDescriptor CreateGlassBrushColorDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.GlassBrush, SKColor> getValue,
        Action<ModernFormsNext.Drawing.GlassBrush, SKColor> setValue,
        bool canEdit,
        int depth,
        string category,
        bool isAdvanced)
        => CreateColorDescriptor(
            $"{path}.{name}",
            displayName,
            category,
            description,
            typeof(SKColor),
            () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.GlassBrush glass
                ? getValue(glass)
                : null,
            color =>
            {
                if (color is null)
                    return (false, "Glass brush colors cannot be null.");

                var brush = CloneGlassBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, color.Value);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            },
            canEdit,
            depth,
            isAdvanced,
            nullable: false);

    private static DesignerPropertyDescriptor CreateGlassBrushBooleanDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.GlassBrush, bool> getValue,
        Action<ModernFormsNext.Drawing.GlassBrush, bool> setValue,
        bool canEdit,
        int depth,
        string category)
        => new()
        {
            Name = name,
            Path = $"{path}.{name}",
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(bool),
            IsReadOnly = !canEdit,
            Depth = depth,
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(typeof(bool)),
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.GlassBrush glass
                && getValue(glass),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This brush property is read-only.");

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(bool), out var value, out var error))
                    return (false, error);

                var brush = CloneGlassBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, (bool)value!);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateLinearGradientPointDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.LinearGradientBrush, SKPoint> getValue,
        Action<ModernFormsNext.Drawing.LinearGradientBrush, SKPoint> setValue,
        bool canEdit,
        int depth,
        string category)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = name,
            Path = $"{path}.{name}",
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(SKPoint),
            IsReadOnly = !canEdit,
            IsAdvanced = true,
            Depth = depth,
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.LinearGradientBrush gradient
                ? getValue(gradient)
                : SKPoint.Empty,
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This brush property is read-only.");

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKPoint), out var value, out var error))
                    return (false, error);

                var brush = CloneLinearGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, (SKPoint)value!);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            }
        };

        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.X", "X", "The horizontal component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.X : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : SKPoint.Empty;
            var brush = CloneLinearGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(value, current.Y));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));
        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.Y", "Y", "The vertical component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.Y : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : SKPoint.Empty;
            var brush = CloneLinearGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(current.X, value));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));

        return root;
    }

    private static DesignerPropertyDescriptor CreateRadialGradientPointDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.RadialGradientBrush, SKPoint> getValue,
        Action<ModernFormsNext.Drawing.RadialGradientBrush, SKPoint> setValue,
        bool canEdit,
        int depth,
        string category)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = name,
            Path = $"{path}.{name}",
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(SKPoint),
            IsReadOnly = !canEdit,
            IsAdvanced = true,
            Depth = depth,
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.RadialGradientBrush gradient
                ? getValue(gradient)
                : new SKPoint(0.5f, 0.5f),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This brush property is read-only.");

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKPoint), out var value, out var error))
                    return (false, error);

                var brush = CloneRadialGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, (SKPoint)value!);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            }
        };

        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.X", "X", "The horizontal component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.X : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : new SKPoint(0.5f, 0.5f);
            var brush = CloneRadialGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(value, current.Y));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));
        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.Y", "Y", "The vertical component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.Y : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : new SKPoint(0.5f, 0.5f);
            var brush = CloneRadialGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(current.X, value));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));

        return root;
    }

    private static DesignerPropertyDescriptor CreateSweepGradientPointDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.SweepGradientBrush, SKPoint> getValue,
        Action<ModernFormsNext.Drawing.SweepGradientBrush, SKPoint> setValue,
        bool canEdit,
        int depth,
        string category)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = name,
            Path = $"{path}.{name}",
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(SKPoint),
            IsReadOnly = !canEdit,
            IsAdvanced = true,
            Depth = depth,
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.SweepGradientBrush gradient
                ? getValue(gradient)
                : new SKPoint(0.5f, 0.5f),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This brush property is read-only.");

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(SKPoint), out var value, out var error))
                    return (false, error);

                var brush = CloneSweepGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, (SKPoint)value!);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            }
        };

        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.X", "X", "The horizontal component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.X : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : new SKPoint(0.5f, 0.5f);
            var brush = CloneSweepGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(value, current.Y));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));
        root.Children.Add(CreateBrushSkPointComponentDescriptor(node, path, $"{name}.Y", "Y", "The vertical component.", valueType, fallbackBrush, () => root.GetValue() is SKPoint point ? point.Y : 0f, value =>
        {
            var current = root.GetValue() is SKPoint point ? point : new SKPoint(0.5f, 0.5f);
            var brush = CloneSweepGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
            setValue(brush, new SKPoint(current.X, value));
            SetStoredValue(node, path, brush, valueType);
            return (true, null);
        }, canEdit, depth + 1, category));

        return root;
    }

    private static DesignerPropertyDescriptor CreateBrushSkPointComponentDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<float> getValue,
        Func<float, (bool Success, string? Error)> setValue,
        bool canEdit,
        int depth,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            getValue,
            setValue,
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateRadialGradientFloatDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.RadialGradientBrush, float> getValue,
        Action<ModernFormsNext.Drawing.RadialGradientBrush, float> setValue,
        Func<float, bool> validate,
        string? validationError,
        bool canEdit,
        int depth,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.RadialGradientBrush gradient
                ? getValue(gradient)
                : 0f,
            value =>
            {
                if (!validate(value))
                    return (false, validationError ?? "The value is not valid.");

                var brush = CloneRadialGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, value);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateSweepGradientFloatDescriptor(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        Func<ModernFormsNext.Drawing.SweepGradientBrush, float> getValue,
        Action<ModernFormsNext.Drawing.SweepGradientBrush, float> setValue,
        Func<float, bool> validate,
        string? validationError,
        bool canEdit,
        int depth,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.SweepGradientBrush gradient
                ? getValue(gradient)
                : 0f,
            value =>
            {
                if (!validate(value))
                    return (false, validationError ?? "The value is not valid.");

                var brush = CloneSweepGradientBrush(GetStoredBrush(node, path, valueType, fallbackBrush));
                setValue(brush, value);
                SetStoredValue(node, path, brush, valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateGradientStopCountDescriptor(
        DesignControlNode node,
        string path,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush,
        string category,
        int depth)
        => new()
        {
            Name = "GradientStops",
            Path = $"{path}.GradientStops",
            DisplayName = "GradientStops",
            Category = category,
            Description = "The number of gradient stops. Editing the stop collection requires a dedicated collection editor.",
            ValueType = typeof(int),
            IsReadOnly = true,
            IsAdvanced = true,
            Depth = depth,
            GetValue = () => GetStoredBrush(node, path, valueType, fallbackBrush) is ModernFormsNext.Drawing.GradientBrush gradient
                ? gradient.GradientStops.Count
                : 0
        };

    private static DesignerPropertyDescriptor CreateStoredImageDescriptor(
        DesignControlNode node,
        string displayName,
        string category,
        string description,
        bool canEdit,
        bool isAdvanced)
        => new()
        {
            Name = "Image",
            Path = "Image",
            DisplayName = displayName,
            Category = category,
            Description = description + " The designer stores this as ImageLocation so the .mfdesign file remains text-based.",
            ValueType = typeof(string),
            IsReadOnly = !canEdit,
            IsAdvanced = isAdvanced,
            HasDialogEditor = canEdit,
            DialogEditor = canEdit ? DesignerPropertyDialogEditors.ImageLocation(node) : null,
            GetValue = () => GetStoredValue(node, "ImageLocation", typeof(string), fallbackValue: null),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This image property is read-only.");

                if (string.IsNullOrWhiteSpace(text))
                    node.Properties["ImageLocation"] = DesignPropertyValue.FromNull();
                else
                    node.Properties["ImageLocation"] = DesignPropertyValue.FromString(text.Trim());

                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateStoredFontDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        Type valueType,
        Font? fallbackFont,
        bool canEdit,
        int depth,
        bool isAdvanced)
    {
        var root = new DesignerPropertyDescriptor
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = valueType,
            IsReadOnly = !canEdit,
            IsAdvanced = isAdvanced,
            HasDialogEditor = canEdit,
            DialogEditor = canEdit ? DesignerPropertyDialogEditors.Font(
                () => GetStoredFont(node, path, valueType, fallbackFont),
                font =>
                {
                    SetStoredValue(node, path, font, valueType);
                    return (true, null);
                }) : null,
            Depth = depth,
            GetValue = () => GetStoredFont(node, path, valueType, fallbackFont),
            CommitText = text =>
            {
                if (!canEdit)
                    return (false, "This font is read-only.");

                if (Nullable.GetUnderlyingType(valueType) is not null
                    && (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase)))
                {
                    node.Properties[path] = DesignPropertyValue.FromNull();
                    return (true, null);
                }

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(Font), out var value, out var error))
                    return (false, error);

                SetStoredValue(node, path, value, valueType);
                return (true, null);
            }
        };

        root.Children.Add(CreateFontNameChild(node, path, valueType, fallbackFont, canEdit, depth + 1, category));
        root.Children.Add(CreateFontSizeChild(node, path, valueType, fallbackFont, canEdit, depth + 1, category));
        root.Children.Add(new DesignerPropertyDescriptor
        {
            Name = "Unit",
            Path = $"{path}.Unit",
            DisplayName = "Unit",
            Category = category,
            Description = "The font size unit. ModernFormsNext stores designer font sizes in points.",
            ValueType = typeof(string),
            IsReadOnly = true,
            Depth = depth + 1,
            GetValue = () => "Point"
        });
        root.Children.Add(CreateFontStyleChild(node, path, "Bold", "Bold", "Whether the font uses bold weight.", valueType, fallbackFont, FontStyle.Bold, canEdit, depth + 1, category));
        root.Children.Add(CreateFontStyleChild(node, path, "Italic", "Italic", "Whether the font uses italic style.", valueType, fallbackFont, FontStyle.Italic, canEdit, depth + 1, category));
        root.Children.Add(CreateFontStyleChild(node, path, "Underline", "Underline", "Whether the font is underlined.", valueType, fallbackFont, FontStyle.Underline, canEdit, depth + 1, category));
        root.Children.Add(CreateFontStyleChild(node, path, "Strikeout", "Strikeout", "Whether the font is drawn with strikeout.", valueType, fallbackFont, FontStyle.Strikeout, canEdit, depth + 1, category));

        return root;
    }

    private static DesignerPropertyDescriptor CreateStoredNullableIntDescriptor(
        DesignControlNode node,
        string path,
        string displayName,
        string category,
        string description,
        int? fallbackValue,
        bool canEdit,
        int depth,
        bool isAdvanced,
        bool requirePositive)
        => new()
        {
            Name = path,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(int?),
            IsReadOnly = !canEdit,
            IsAdvanced = isAdvanced,
            Depth = depth,
            GetValue = () => GetStoredValue(node, path, typeof(int?), fallbackValue),
            CommitText = text =>
            {
                if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                {
                    node.Properties[path] = DesignPropertyValue.FromNull();
                    return (true, null);
                }

                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                var intValue = (int)value!;

                if (requirePositive && intValue <= 0)
                    return (false, "The value must be greater than zero.");

                if (!requirePositive && intValue < 0)
                    return (false, "The value cannot be negative.");

                node.Properties[path] = DesignPropertyValue.FromInt32(intValue);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateStoredRectangleChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<System.Drawing.Rectangle, int> getValue,
        Func<System.Drawing.Rectangle, int, System.Drawing.Rectangle> update,
        bool requireNonNegative,
        int depth,
        bool canEdit,
        string category)
        => CreateIntChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredRectangle(node, path, valueType, fallbackValue)),
            value =>
            {
                if (requireNonNegative && value < 0)
                    return (false, "The value cannot be negative.");

                SetStoredValue(node, path, update(GetStoredRectangle(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredSizeChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<System.Drawing.Size, int> getValue,
        Func<System.Drawing.Size, int, System.Drawing.Size> update,
        int depth,
        bool canEdit,
        string category)
        => CreateIntChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredSize(node, path, valueType, fallbackValue)),
            value =>
            {
                if (value < 0)
                    return (false, "The value cannot be negative.");

                SetStoredValue(node, path, update(GetStoredSize(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredPointChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<System.Drawing.Point, int> getValue,
        Func<System.Drawing.Point, int, System.Drawing.Point> update,
        int depth,
        bool canEdit,
        string category)
        => CreateIntChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredPoint(node, path, valueType, fallbackValue)),
            value =>
            {
                SetStoredValue(node, path, update(GetStoredPoint(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredPaddingChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<Padding, int> getValue,
        Func<Padding, int, Padding> update,
        int depth,
        bool canEdit,
        string category)
        => CreateIntChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredPadding(node, path, valueType, fallbackValue)),
            value =>
            {
                if (value < 0)
                    return (false, "Padding and margin values cannot be negative.");

                SetStoredValue(node, path, update(GetStoredPadding(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredPointFChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<System.Drawing.PointF, float> getValue,
        Func<System.Drawing.PointF, float, System.Drawing.PointF> update,
        int depth,
        bool canEdit,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredPointF(node, path, valueType, fallbackValue)),
            value =>
            {
                SetStoredValue(node, path, update(GetStoredPointF(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredSkPointChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<SKPoint, float> getValue,
        Func<SKPoint, float, SKPoint> update,
        int depth,
        bool canEdit,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredSkPoint(node, path, valueType, fallbackValue)),
            value =>
            {
                SetStoredValue(node, path, update(GetStoredSkPoint(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredSkSizeChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<SKSize, float> getValue,
        Func<SKSize, float, SKSize> update,
        int depth,
        bool canEdit,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredSkSize(node, path, valueType, fallbackValue)),
            value =>
            {
                if (value < 0)
                    return (false, "Size values cannot be negative.");

                SetStoredValue(node, path, update(GetStoredSkSize(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateStoredSkRectChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        object? fallbackValue,
        Func<SKRect, float> getValue,
        Func<SKRect, float, SKRect> update,
        int depth,
        bool canEdit,
        string category)
        => CreateFloatChild(
            name,
            $"{path}.{name}",
            displayName,
            category,
            description,
            () => getValue(GetStoredSkRect(node, path, valueType, fallbackValue)),
            value =>
            {
                SetStoredValue(node, path, update(GetStoredSkRect(node, path, valueType, fallbackValue), value), valueType);
                return (true, null);
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateColorComponentChild(
        string path,
        string name,
        string description,
        Func<SKColor?> getColor,
        Func<SKColor?, (bool Success, string? Error)> setColor,
        Func<SKColor, byte> getValue,
        Func<SKColor, int, SKColor> update,
        bool canEdit,
        int depth,
        string category)
        => CreateIntChild(
            name,
            $"{path}.{name}",
            name,
            category,
            description,
            () => getValue(getColor() ?? new SKColor(0, 0, 0, 255)),
            value =>
            {
                if (value is < 0 or > 255)
                    return (false, "Color channel values must be between 0 and 255.");

                return setColor(update(getColor() ?? new SKColor(0, 0, 0, 255), value));
            },
            depth,
            isReadOnly: !canEdit);

    private static DesignerPropertyDescriptor CreateFontNameChild(
        DesignControlNode node,
        string path,
        Type valueType,
        Font? fallbackFont,
        bool canEdit,
        int depth,
        string category)
        => new()
        {
            Name = "Name",
            Path = $"{path}.Name",
            DisplayName = "Name",
            Category = category,
            Description = "The font family name.",
            ValueType = typeof(string),
            IsReadOnly = !canEdit,
            Depth = depth,
            GetValue = () => GetEffectiveFont(node, path, valueType, fallbackFont).FamilyName,
            CommitText = text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                    return (false, "The font family name cannot be empty.");

                var current = GetEffectiveFont(node, path, valueType, fallbackFont);
                SetStoredValue(node, path, new Font(text.Trim(), current.SizeInPoints, current.Style), valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateFontSizeChild(
        DesignControlNode node,
        string path,
        Type valueType,
        Font? fallbackFont,
        bool canEdit,
        int depth,
        string category)
        => new()
        {
            Name = "Size",
            Path = $"{path}.Size",
            DisplayName = "Size",
            Category = category,
            Description = "The font size in points.",
            ValueType = typeof(float),
            IsReadOnly = !canEdit,
            Depth = depth,
            GetValue = () => GetEffectiveFont(node, path, valueType, fallbackFont).SizeInPoints,
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(float), out var value, out var error))
                    return (false, error);

                var size = (float)value!;

                if (size <= 0)
                    return (false, "The font size must be greater than zero.");

                var current = GetEffectiveFont(node, path, valueType, fallbackFont);
                SetStoredValue(node, path, new Font(current.FamilyName, size, current.Style), valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateFontStyleChild(
        DesignControlNode node,
        string path,
        string name,
        string displayName,
        string description,
        Type valueType,
        Font? fallbackFont,
        FontStyle style,
        bool canEdit,
        int depth,
        string category)
        => new()
        {
            Name = name,
            Path = $"{path}.{name}",
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(bool),
            IsReadOnly = !canEdit,
            Depth = depth,
            StandardValues = DesignerPropertyValueEditor.GetStandardValues(typeof(bool)),
            GetValue = () => GetEffectiveFont(node, path, valueType, fallbackFont).Style.HasFlag(style),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(bool), out var value, out var error))
                    return (false, error);

                var enabled = (bool)value!;
                var current = GetEffectiveFont(node, path, valueType, fallbackFont);
                var nextStyle = enabled ? current.Style | style : current.Style & ~style;
                SetStoredValue(node, path, new Font(current.FamilyName, current.SizeInPoints, nextStyle), valueType);
                return (true, null);
            }
        };

    private static DesignerPropertyDescriptor CreateIntChild(
        string name,
        string path,
        string displayName,
        string category,
        string description,
        Func<int> getValue,
        Func<int, (bool Success, string? Error)> commit,
        int depth,
        bool isReadOnly = false)
        => new()
        {
            Name = name,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(int),
            IsReadOnly = isReadOnly,
            Depth = depth,
            GetValue = () => getValue(),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(int), out var value, out var error))
                    return (false, error);

                return commit((int)value!);
            }
        };

    private static DesignerPropertyDescriptor CreateFloatChild(
        string name,
        string path,
        string displayName,
        string category,
        string description,
        Func<float> getValue,
        Func<float, (bool Success, string? Error)> commit,
        int depth,
        bool isReadOnly = false)
        => new()
        {
            Name = name,
            Path = path,
            DisplayName = displayName,
            Category = category,
            Description = description,
            ValueType = typeof(float),
            IsReadOnly = isReadOnly,
            Depth = depth,
            GetValue = () => getValue(),
            CommitText = text =>
            {
                if (!DesignerPropertyValueEditor.TryConvert(text, typeof(float), out var value, out var error))
                    return (false, error);

                return commit((float)value!);
            }
        };

    private static object? GetStoredValue(DesignControlNode node, string path, Type valueType, object? fallbackValue)
    {
        if (!node.Properties.TryGetValue(path, out var value))
            return fallbackValue;

        try
        {
            return DesignerPropertyValueEditor.FromDesignPropertyValue(value, valueType);
        }
        catch
        {
            return fallbackValue;
        }
    }

    private static void SetStoredValue(DesignControlNode node, string path, object? value, Type valueType)
        => node.Properties[path] = DesignerPropertyValueEditor.ToDesignPropertyValue(value, valueType);

    private static System.Drawing.Rectangle GetStoredRectangle(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is System.Drawing.Rectangle rectangle
            ? rectangle
            : System.Drawing.Rectangle.Empty;

    private static System.Drawing.Size GetStoredSize(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is System.Drawing.Size size
            ? size
            : System.Drawing.Size.Empty;

    private static System.Drawing.Point GetStoredPoint(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is System.Drawing.Point point
            ? point
            : System.Drawing.Point.Empty;

    private static System.Drawing.PointF GetStoredPointF(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is System.Drawing.PointF point
            ? point
            : System.Drawing.PointF.Empty;

    private static Padding GetStoredPadding(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is Padding padding
            ? padding
            : Padding.Empty;

    private static SKPoint GetStoredSkPoint(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is SKPoint point
            ? point
            : SKPoint.Empty;

    private static SKSize GetStoredSkSize(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is SKSize size
            ? size
            : SKSize.Empty;

    private static SKRect GetStoredSkRect(DesignControlNode node, string path, Type valueType, object? fallbackValue)
        => GetStoredValue(node, path, valueType, fallbackValue) is SKRect rect
            ? rect
            : SKRect.Empty;

    private static SKColor? GetStoredColor(DesignControlNode node, string path, Type valueType, SKColor? fallbackColor)
        => GetStoredValue(node, path, valueType, fallbackColor) is SKColor color
            ? color
            : fallbackColor;

    private static Font? GetStoredFont(DesignControlNode node, string path, Type valueType, Font? fallbackFont)
        => GetStoredValue(node, path, valueType, fallbackFont) as Font;

    private static Font GetEffectiveFont(DesignControlNode node, string path, Type valueType, Font? fallbackFont)
        => GetStoredFont(node, path, valueType, fallbackFont) ?? new Font("Segoe UI", 9);

    private static ModernFormsNext.Drawing.Brush? GetStoredBrush(
        DesignControlNode node,
        string path,
        Type valueType,
        ModernFormsNext.Drawing.Brush? fallbackBrush)
        => GetStoredValue(node, path, valueType, fallbackBrush) as ModernFormsNext.Drawing.Brush;

    private static SKColor? GetBrushColor(object? brush)
        => brush is ModernFormsNext.Drawing.SolidColorBrush solidBrush ? solidBrush.Color : null;

    private static ModernFormsNext.Drawing.SolidColorBrush CreateSolidBrushPreservingCommonProperties(
        ModernFormsNext.Drawing.Brush? source,
        SKColor color)
        => new(color)
        {
            Opacity = source?.Opacity ?? 1f,
            Transform = source?.Transform ?? System.Numerics.Matrix3x2.Identity
        };

    private static ModernFormsNext.Drawing.GlassBrush CloneGlassBrush(ModernFormsNext.Drawing.Brush? brush)
    {
        var source = brush as ModernFormsNext.Drawing.GlassBrush ?? new ModernFormsNext.Drawing.GlassBrush();
        return new ModernFormsNext.Drawing.GlassBrush
        {
            TintColor = source.TintColor,
            SecondaryTintColor = source.SecondaryTintColor,
            HighlightColor = source.HighlightColor,
            BorderColor = source.BorderColor,
            ShowHighlight = source.ShowHighlight,
            ShowInnerBorder = source.ShowInnerBorder,
            Opacity = source.Opacity,
            Transform = source.Transform
        };
    }

    private static ModernFormsNext.Drawing.LinearGradientBrush CloneLinearGradientBrush(ModernFormsNext.Drawing.Brush? brush)
    {
        var source = brush as ModernFormsNext.Drawing.LinearGradientBrush ?? new ModernFormsNext.Drawing.LinearGradientBrush();
        var clone = new ModernFormsNext.Drawing.LinearGradientBrush
        {
            StartPoint = source.StartPoint,
            EndPoint = source.EndPoint,
            SpreadMode = source.SpreadMode,
            Opacity = source.Opacity,
            Transform = source.Transform
        };

        CopyGradientStops(source, clone);
        return clone;
    }

    private static ModernFormsNext.Drawing.RadialGradientBrush CloneRadialGradientBrush(ModernFormsNext.Drawing.Brush? brush)
    {
        var source = brush as ModernFormsNext.Drawing.RadialGradientBrush ?? new ModernFormsNext.Drawing.RadialGradientBrush();
        var clone = new ModernFormsNext.Drawing.RadialGradientBrush
        {
            Center = source.Center,
            GradientOrigin = source.GradientOrigin,
            Radius = source.Radius,
            SpreadMode = source.SpreadMode,
            Opacity = source.Opacity,
            Transform = source.Transform
        };

        CopyGradientStops(source, clone);
        return clone;
    }

    private static ModernFormsNext.Drawing.SweepGradientBrush CloneSweepGradientBrush(ModernFormsNext.Drawing.Brush? brush)
    {
        var source = brush as ModernFormsNext.Drawing.SweepGradientBrush ?? new ModernFormsNext.Drawing.SweepGradientBrush();
        var clone = new ModernFormsNext.Drawing.SweepGradientBrush
        {
            Center = source.Center,
            StartAngle = source.StartAngle,
            EndAngle = source.EndAngle,
            SpreadMode = source.SpreadMode,
            Opacity = source.Opacity,
            Transform = source.Transform
        };

        CopyGradientStops(source, clone);
        return clone;
    }

    private static void CopyGradientStops(
        ModernFormsNext.Drawing.GradientBrush source,
        ModernFormsNext.Drawing.GradientBrush target)
    {
        foreach (var stop in source.GradientStops)
            target.GradientStops.Add(new ModernFormsNext.Drawing.GradientStop(stop.Color, stop.Offset));
    }
}
