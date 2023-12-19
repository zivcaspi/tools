using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#region class FindSubstringInStream
public static class FindSubstringInStream
{
    /// <summary>
    /// Locates the substring <paramref name="substring"/> in a stream of characters produced by <paramref name="getNextCharacterOrMinusOne"/>.
    /// </summary>
    /// <param name="getNextCharacterOrMinusOne">A function that returns the stream of characters as integers (char values),
    /// yielding -1 when there are no other characters to return.</param>
    /// <param name="substring">The substring to search for.</param>
    /// <returns>The first position (0-based) in the stream at which the first matching substring can be found.
    /// -1 is returned if there's no match.</returns>
    /// <remarks>
    /// Trivial implementation using DFA</remarks>
    public static long IndexOf(Func<int> getNextCharacterOrMinusOne, string substring)
    {
        // TODO: Validate stream, substring make sense
        var n = substring.Length;

        var prev = new Indexes();
        prev.Init(n);

        var next = new Indexes();
        next.Init(n);

        var pos = -1;
        while (true)
        {
            var ch = getNextCharacterOrMinusOne();
            pos++;
            if (ch <= 0)
            {
                return -1;
            }

            prev.Push(0);
            foreach (var i in prev)
            {
                if (ch == substring[i])
                {
                    if (i + 1 == n)
                    {
                        return pos + 1 - n;
                    }
                    else
                    {
                        next.Push(i + 1);
                    }
                }
            }
            prev.CopyFromAndClear(ref next);
        }
    }

    private struct Indexes : IEnumerable<int>
    {
        public int[] m_array;
        public int m_count;

        public void Init(int n)
        {
            m_array = new int[n];
            m_count = 0;
        }

        public void Push(int i)
        {
            m_array[m_count++] = i;
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < m_count; i++)
            {
                yield return m_array[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new Exception();
        }

        public void CopyFromAndClear(ref Indexes rhs)
        {
            var temp = rhs.m_array;

            m_array = rhs.m_array;
            m_count = rhs.m_count;

            rhs.m_array = temp;
            rhs.m_count = 0;
        }
    }

    public static void Test()
    {
        var pos = IndexOf(Produce("bbbaaa"), "aaa");
        // pos here should be 3 (position at which the match started)
    }

    private static Func<int> Produce(string s)
    {
        var pos = 0;
        return () =>
        {
            if (pos < s.Length)
            {
                return s[pos++];
            }
            return -1;
        };
    }
}
#endregion

