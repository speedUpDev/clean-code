﻿using Markdown.Tags;

namespace Markdown.Finder;

public class MarkdownTagFinder : ITagFinder
{
    private Stack<TagType> needClosingTag = new();
    private Stack<int> needClosingTagIndexes = new();
    private Queue<TagType> poppedTags = new();
    private List<Tag> uncertainTags = new();
    private int realIndex;
    private string origin;
    private List<Tag> foundedTags = new();

    private Dictionary<TagType, TagType> differentType = new()
    {
        { TagType.Bold, TagType.Italic },
        { TagType.Italic, TagType.Bold }
    };

    public IEnumerable<Tag> FindTags(string line)
    {
        Reset(line);
        if (line.StartsWith("# "))
        {
            foundedTags.Add(new Tag(new TagPosition(0, line.Length - 1), TagType.Header));
        }
        else if (line.StartsWith("* "))
        {
            foundedTags.Add(new Tag(new TagPosition(0, line.Length - 1), TagType.MarkedListItem));
        }

        for (; realIndex < line.Length; realIndex++)
        {
            var sym = line[realIndex];
            switch (sym)
            {
                case '\\':
                    if (realIndex < line.Length - 1 && (line[realIndex + 1] == '\\' || line[realIndex + 1] == '_'))
                    {
                        foundedTags.Add(new Tag(new TagPosition(realIndex, realIndex), TagType.EscapedSymbol));
                        realIndex++;
                    }

                    continue;
                case '_':
                {
                    var tagType = GetTagType(realIndex);
                    if (tagType == TagType.NotATag)
                    {
                        continue;
                    }

                    HandleTag(tagType, realIndex);
                    break;
                }
            }
        }

        foundedTags.AddRange(uncertainTags);
        return foundedTags;
    }

    private void Reset(string line)
    {
        needClosingTag.Clear();
        needClosingTagIndexes.Clear();
        uncertainTags.Clear();
        poppedTags.Clear();
        realIndex = 0;
        foundedTags = new();
        origin = line;
    }

    private void HandleTag(TagType tagType, int i)
    {
        var (popped, index) = FindOpeningTag(tagType, i);
        if (popped == TagType.NotATag)
        {
            if (i < origin.Length - 1 && !char.IsWhiteSpace(origin[i + 1]))
            {
                needClosingTag.Push(tagType);
                needClosingTagIndexes.Push(i);
            }
        }
        else
        {
            var tag = new Tag(new TagPosition(index, i), tagType);
            if (CanBeAdded(tag))
            {
                if (tag.Type == TagType.Italic)
                {
                    uncertainTags.Clear();
                }

                foundedTags.Add(tag);
            }
            else if (poppedTags.Count > 0 && poppedTags.Peek() == tagType)
            {
                poppedTags.Dequeue();
                needClosingTag.Push(tagType);
                needClosingTagIndexes.Push(i);
            }
        }
    }

    private bool CanBeAdded(Tag tag)
    {
        var shift = tag.Type == TagType.Italic ? 1 : 2;
        var subString = origin.Substring(tag.Position.Start + 1, tag.Position.End - tag.Position.Start - 1);
        if (char.IsDigit(origin[tag.Position.End - shift]) && origin.Length > tag.Position.End + 1 &&
            !char.IsWhiteSpace(origin[tag.Position.End + 1]) ||
            char.IsWhiteSpace(origin[tag.Position.End - shift]) ||
            poppedTags.Dequeue() == differentType[tag.Type])
        {
            return false;
        }

        if (tag.Position.Start - shift > 0 && char.IsDigit(origin[tag.Position.Start + 1]) &&
            !char.IsWhiteSpace(origin[tag.Position.Start - shift]))
        {
            return false;
        }

        if (tag.Position.End < origin.Length - 1 && !char.IsWhiteSpace(origin[tag.Position.End + 1]) &&
            subString.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (tag.Type == TagType.Bold && needClosingTag.Contains(differentType[tag.Type]))
        {
            uncertainTags.Add(tag);
            return false;
        }

        return true;
    }

    private (TagType, int) FindOpeningTag(TagType tagType, int i)
    {
        var popped = TagType.NotATag;
        var index = i;
        while (needClosingTag.Contains(tagType))
        {
            popped = needClosingTag.Pop();
            index = needClosingTagIndexes.Pop();
            poppedTags.Enqueue(popped);
        }

        return (popped, index);
    }

    private TagType GetTagType(int i)
    {
        if (i < origin.Length - 1 && origin[i + 1] == '_')
        {
            if (i < origin.Length - 2 && origin[i + 2] == '_')
            {
                while (i < origin.Length && origin[i] == '_')
                {
                    i++;
                }

                realIndex = i;
                return TagType.NotATag;
            }

            realIndex++;
            return TagType.Bold;
        }

        return TagType.Italic;
    }
}