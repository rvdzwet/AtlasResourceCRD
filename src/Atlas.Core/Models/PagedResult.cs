using System;
using System.Collections.Generic;

namespace Atlas.Core.Models;

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalPages { get; set; } = 1;
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedResult() { }

    public PagedResult(List<T> items, int totalItems, int page, int pageSize)
    {
        Items = items ?? new List<T>();
        TotalItems = totalItems;
        Page = Math.Max(1, page);
        PageSize = Math.Max(1, pageSize);
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));
    }
}
