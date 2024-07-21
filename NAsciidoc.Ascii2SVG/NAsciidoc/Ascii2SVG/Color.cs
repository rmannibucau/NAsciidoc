namespace NAsciidoc.Ascii2SVG;

public record Color(string C)
{
    public int[] ParseHexColor()
    {
        switch (C.Length)
        {
            case 3:
                return
                [
                    Convert.ToInt32(C[0].ToString(), 16) * 17,
                    Convert.ToInt32(C[1].ToString(), 16) * 17,
                    Convert.ToInt32(C[2].ToString(), 16) * 17
                ];
            case 6:

                return
                [
                    Convert.ToInt32(C[0..2], 16),
                    Convert.ToInt32(C[2..4], 16),
                    Convert.ToInt32(C[4..6], 16)
                ];
            default:
                throw new ArgumentException($"unknown color: '{C}'", nameof(C));
        }
    }

    public int[] ColorToRGB()
    {
        if (C[0] == '#')
        {
            return new Color(C[1..]).ParseHexColor();
        }
        throw new ArgumentException($"unknown color type: '{C}'", nameof(C));
    }

    public string TextColor()
    {
        var rgb = new Color(C).ColorToRGB();
        var brightness = (rgb[0] * 299 + rgb[1] * 587 + rgb[2] * 114) / 1000;
        var difference = rgb.Sum();
        if (brightness < 125 && difference < 500)
        {
            return "#fff";
        }
        return "#000";
    }
}
