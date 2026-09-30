using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Common
{
    public class PagedResult <T> (IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize)
    {
        public IReadOnlyList<T> Items { get; } = Items;
        public int TotalCount { get; } = TotalCount;
        public int PageNumber { get; } = PageNumber;
        public int PageSize { get; } = PageSize;
        public int TotalPages { get; } = (int)Math.Ceiling((double)TotalCount / PageSize);
        
        //public PagedResult<T> WithItems(IReadOnlyList<T> items) => new PagedResult<T>(items, TotalCount, PageNumber, PageSize);
    }
}
