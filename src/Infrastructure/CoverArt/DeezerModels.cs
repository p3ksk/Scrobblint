namespace Scrobblint.Infrastructure.CoverArt;

public class DeezerSearchResult<T>
{
    public List<T> Data { get; set; } = new();
    public int Total { get; set; }
}

public class DeezerAlbum
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string CoverSmall { get; set; } = "";
    public string CoverMedium { get; set; } = "";
    public DeezerArtistRef Artist { get; set; } = new();
}

public class DeezerArtist
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string PictureSmall { get; set; } = "";
}

public class DeezerArtistRef
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
}
