using System.Collections;
using System.Collections.Generic;

namespace cn.efunstudio.psdreader.PsdParser
{
    public interface IProperties : IEnumerable<KeyValuePair<string, object>>, IEnumerable
    {
        object this[string property] { get; }

        int Count { get; }

        bool Contains(string property);
    }
}
