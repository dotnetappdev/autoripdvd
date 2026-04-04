using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using AutoRipDVD.Models;

namespace AutoRipDVD.ViewModels;

/// <summary>
/// Discriminator for node types in the MakeMKV-style disc tree.
/// </summary>
public enum DiscNodeType
{
    Disc,
    Title,
    Chapters,
    VideoTrack,
    AudioTrack,
    SubtitleTrack
}

/// <summary>
/// Base class for all nodes in the MakeMKV-style disc/title/track tree.
/// All display properties are on this class so a single DataTemplate can handle all types.
/// </summary>
public abstract partial class DiscTreeNode : ObservableObject
{
    // ── Checkbox state ────────────────────────────────────────────────────────

    [ObservableProperty] private bool _isChecked = true;

    // ── Abstract display properties ───────────────────────────────────────────

    public abstract DiscNodeType NodeType { get; }

    /// <summary>Short type label shown in the left "Type" column: "Title", "Audio", "Subtitles", etc.</summary>
    public abstract string DisplayType { get; }

    /// <summary>Description shown in the right "Description" column.</summary>
    public abstract string DisplayDescription { get; }

    /// <summary>Whether this node has an include/exclude checkbox.</summary>
    public abstract bool IsCheckable { get; }

    /// <summary>Segoe MDL2 Assets glyph character for the node icon.</summary>
    public abstract string IconGlyph { get; }

    // ── Derived display helpers (no converter needed in XAML) ─────────────────

    /// <summary>Opacity 1.0 for checkable nodes, 0.0 for non-checkable (preserves layout alignment).</summary>
    public double CheckBoxOpacity => IsCheckable ? 1.0 : 0.0;

    /// <summary>IsHitTestVisible false hides click handling on invisible checkboxes.</summary>
    public bool CheckBoxInteractive => IsCheckable;

    // ── Child nodes ───────────────────────────────────────────────────────────

    public ObservableCollection<DiscTreeNode> Children { get; } = new();

    // ── Info panel ────────────────────────────────────────────────────────────

    /// <summary>Multi-line info text shown in the right-panel "Info" section.</summary>
    public abstract string BuildInfoText();
}

// ── Disc root node ────────────────────────────────────────────────────────────

public sealed class DiscRootNode : DiscTreeNode
{
    private readonly DiscType _discType;
    private readonly string   _volumeName;

    public override DiscNodeType NodeType => DiscNodeType.Disc;

    public override string DisplayType
        => _discType == DiscType.BluRay ? "Blu-ray disc" : "DVD";

    public override string DisplayDescription => _volumeName;
    public override bool   IsCheckable        => false;
    public override string IconGlyph          => "\uE958"; // Optical disc

    public DiscRootNode(DiscType discType, string volumeName)
    {
        _discType   = discType;
        _volumeName = volumeName;
        IsChecked   = true;
    }

    public override string BuildInfoText()
        => $"Disc type: {DisplayType}\nVolume name: {_volumeName}";
}

// ── Title node ────────────────────────────────────────────────────────────────

public sealed partial class TitleTreeNode : DiscTreeNode
{
    public TitleInfo Title { get; }

    public override DiscNodeType NodeType => DiscNodeType.Title;

    public override string DisplayType => "Title";

    public override string DisplayDescription
        => $"{Title.Name} - {Title.ChapterCount} chapter(s), {Title.FormattedSize}";

    public override bool   IsCheckable => true;
    public override string IconGlyph   => "\uE8B7"; // Film / entertainment

    public TitleTreeNode(TitleInfo title)
    {
        Title = title;

        // Propagate to audio/subtitle children when title is checked/unchecked
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(IsChecked)) return;
            foreach (var child in Children)
            {
                if (child is AudioTrackNode or SubtitleTrackNode)
                    child.IsChecked = IsChecked;
            }
        };
    }

    public override string BuildInfoText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Title information");
        sb.AppendLine($"Name: {Title.Name}");

        if (!string.IsNullOrEmpty(Title.FileName))
            sb.AppendLine($"Source file name: {Title.FileName}");

        sb.AppendLine($"Duration: {Title.FormattedDuration}");
        sb.AppendLine($"Size: {Title.FormattedSize}");

        if (Title.ChapterCount > 0)
            sb.AppendLine($"Chapter count: {Title.ChapterCount}");
        if (Title.SegmentCount > 0)
            sb.AppendLine($"Segment count: {Title.SegmentCount}");
        if (!string.IsNullOrEmpty(Title.SegmentMap))
            sb.AppendLine($"Segment map: {Title.SegmentMap}");
        if (!string.IsNullOrEmpty(Title.FileName))
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(Title.FileName);
            sb.AppendLine($"Output file name: {baseName}.mkv");
        }
        if (!string.IsNullOrEmpty(Title.Comment))
            sb.AppendLine($"Comment: {Title.Comment}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Audio track child nodes for building job settings.</summary>
    public IEnumerable<AudioTrackNode> AudioNodes
        => Children.OfType<AudioTrackNode>();

    /// <summary>Subtitle track child nodes for building job settings.</summary>
    public IEnumerable<SubtitleTrackNode> SubtitleNodes
        => Children.OfType<SubtitleTrackNode>();
}

// ── Chapters node (non-checkable, informational) ──────────────────────────────

public sealed class ChaptersNode : DiscTreeNode
{
    private readonly int _count;

    public override DiscNodeType NodeType        => DiscNodeType.Chapters;
    public override string       DisplayType     => "Chapters";
    public override string       DisplayDescription => $"{_count} chapter(s)";
    public override bool         IsCheckable     => false;
    public override string       IconGlyph       => "\uE8FD"; // Bookmark

    public ChaptersNode(int chapterCount) => _count = chapterCount;

    public override string BuildInfoText()
        => $"Chapter markers\n{_count} chapter(s) included automatically.";
}

// ── Video track node (non-checkable) ─────────────────────────────────────────

public sealed class VideoTrackNode : DiscTreeNode
{
    private readonly string _codec;
    private readonly string _resolution;

    public override DiscNodeType NodeType        => DiscNodeType.VideoTrack;
    public override string       DisplayType     => "Video";
    public override string       DisplayDescription
        => string.IsNullOrEmpty(_resolution) ? _codec : $"{_codec}, {_resolution}";
    public override bool         IsCheckable => false;
    public override string       IconGlyph   => "\uE714"; // Video camera

    public VideoTrackNode(string codec, string resolution)
    {
        _codec      = codec;
        _resolution = resolution;
    }

    public override string BuildInfoText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Video stream");
        if (!string.IsNullOrEmpty(_codec))      sb.AppendLine($"Codec: {_codec}");
        if (!string.IsNullOrEmpty(_resolution)) sb.AppendLine($"Resolution: {_resolution}");
        return sb.ToString().TrimEnd();
    }
}

// ── Audio track node (checkable) ─────────────────────────────────────────────

public sealed partial class AudioTrackNode : DiscTreeNode
{
    public int    TrackIndex   { get; }
    private readonly string _description;

    public override DiscNodeType NodeType        => DiscNodeType.AudioTrack;
    public override string       DisplayType     => "Audio";
    public override string       DisplayDescription => _description;
    public override bool         IsCheckable     => true;
    public override string       IconGlyph       => "\uE8D6"; // Volume/speaker

    public AudioTrackNode(int trackIndex, string description)
    {
        TrackIndex   = trackIndex;
        _description = description;
        IsChecked    = true;
    }

    public override string BuildInfoText()
        => $"Audio track {TrackIndex + 1}\n{_description}";
}

// ── Subtitle track node (checkable) ──────────────────────────────────────────

public sealed partial class SubtitleTrackNode : DiscTreeNode
{
    public int    TrackIndex   { get; }
    private readonly string _description;

    public override DiscNodeType NodeType        => DiscNodeType.SubtitleTrack;
    public override string       DisplayType     => "Subtitles";
    public override string       DisplayDescription => _description;
    public override bool         IsCheckable     => true;
    public override string       IconGlyph       => "\uED1E"; // Subtitles/captions

    public SubtitleTrackNode(int trackIndex, string description)
    {
        TrackIndex   = trackIndex;
        _description = description;
        IsChecked    = true;
    }

    public override string BuildInfoText()
        => $"Subtitle track {TrackIndex + 1}\n{_description}";
}
