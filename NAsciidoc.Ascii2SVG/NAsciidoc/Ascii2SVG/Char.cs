using System.Globalization;

namespace NAsciidoc.Ascii2SVG;

public record Char(char Value)
{
    public bool IsObjectStartTag()
    {
        return Value == '[';
    }

    public bool IsObjectEndTag()
    {
        return Value == ']';
    }

    public bool IsTagDefinitionSeparator()
    {
        return Value == ':';
    }

    public bool IsTextStart()
    {
        return IsObjectStartTag()
            || char.IsLetter(Value)
            || char.IsDigit(Value)
            || char.GetUnicodeCategory(Value) == UnicodeCategory.OtherSymbol;
    }

    public bool IsTextCont()
    {
        return !char.IsControl(Value)
            && Value != 0xFFFF
            && char.GetUnicodeCategory(Value) != UnicodeCategory.OtherNotAssigned;
    }

    public bool IsSpace()
    {
        return char.IsWhiteSpace(Value);
    }

    public bool IsPathStart()
    {
        return (
                IsCorner()
                || IsHorizontal()
                || IsVertical()
                || IsArrowHorizontalLeft()
                || IsArrowVerticalUp()
                || IsDiagonal()
            )
            && !IsTick()
            && !IsDot();
    }

    public bool IsCorner()
    {
        return Value == '.' || Value == '\'' || Value == '+';
    }

    public bool IsRoundedCorner()
    {
        return Value == '.' || Value == '\'';
    }

    public bool IsDashedHorizontal()
    {
        return Value == '=';
    }

    public bool IsHorizontal()
    {
        return IsDashedHorizontal() || IsTick() || IsDot() || Value == '-';
    }

    public bool IsDashedVertical()
    {
        return Value == ':';
    }

    public bool IsVertical()
    {
        return IsDashedVertical() || IsTick() || IsDot() || Value == '|';
    }

    public bool IsDashed()
    {
        return IsDashedHorizontal() || IsDashedVertical();
    }

    public bool IsArrowHorizontalLeft()
    {
        return Value == '<';
    }

    public bool IsArrowHorizontal()
    {
        return IsArrowHorizontalLeft() || Value == '>';
    }

    public bool IsArrowVerticalUp()
    {
        return Value == '^';
    }

    public bool IsArrowVertical()
    {
        return IsArrowVerticalUp() || Value == 'v';
    }

    public bool IsArrow()
    {
        return IsArrowHorizontal() || IsArrowVertical();
    }

    public bool IsDiagonalNorthEast()
    {
        return Value == '/';
    }

    public bool IsDiagonalSouthEast()
    {
        return Value == '\\';
    }

    public bool IsDiagonal()
    {
        return IsDiagonalNorthEast() || IsDiagonalSouthEast();
    }

    public bool IsTick()
    {
        return Value == 'x';
    }

    public bool IsDot()
    {
        return Value == 'o';
    }

    public bool CanDiagonalFrom(Char from)
    {
        if (from.IsArrowVertical() || from.IsCorner())
        {
            return IsDiagonal();
        }
        if (from.IsDiagonal())
        {
            return IsDiagonal()
                || IsCorner()
                || IsArrowVertical()
                || IsHorizontal()
                || IsVertical();
        }
        if (from.IsHorizontal() || from.IsVertical())
        {
            return IsDiagonal();
        }
        return false;
    }

    public bool CanHorizontal()
    {
        return IsHorizontal() || IsCorner() || IsArrowHorizontal();
    }

    public bool CanVertical()
    {
        return IsVertical() || IsCorner() || IsArrowVertical();
    }
}
