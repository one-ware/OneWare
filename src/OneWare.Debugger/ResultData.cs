// ResultData.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Collections;
using System.Text;

namespace OneWare.Debugger;

public class ResultData : IEnumerable
{
    private object[] _array;
    private bool _isArrayProperty;
    private Hashtable _props;

    public int Count
    {
        get
        {
            if (_array != null)
                return _array.Length;
            if (_props != null)
                return _props.Count;
            return 0;
        }
    }

    public IEnumerator GetEnumerator()
    {
        if (_props != null)
            return _props.Values.GetEnumerator();
        if (_array != null)
            return _array.GetEnumerator();
        return new object[0].GetEnumerator();
    }

    public string GetValue(string name)
    {
        return (string)_props[name];
    }

    public int GetInt(string name)
    {
        var value = GetValue(name);

        if (value == null) return 0;

        return int.Parse(value);
    }

    public string GetValue(int index)
    {
        return (string)_array[index];
    }

    public ResultData GetObject(string name)
    {
        return (ResultData)_props[name];
    }

    public ResultData GetObject(int index)
    {
        return (ResultData)_array[index];
    }

    protected object[] GetAllValues(string name)
    {
        var ob = _props[name];
        if (ob == null)
            return new object[0];
        var rd = ob as ResultData;
        if (rd != null && rd._isArrayProperty)
            return rd._array;
        return new[] { ob };
    }

    protected void ReadResults(string str, int pos)
    {
        ReadTuple(str, ref pos, this);
    }

    private void ReadResult(string str, ref int pos, out string name, out object value)
    {
        name = null;
        value = null;

        name = ReadString(str, '=', ref pos);
        ReadChar(str, ref pos, '=');
        value = ReadValue(str, ref pos);
    }

    private string ReadString(string str, char term, ref int pos)
    {
        var sb = new StringBuilder();
        while (pos < str.Length && str[pos] != term)
        {
            if (str[pos] == '\\')
            {
                pos++;

                sb.Append("\\");
                if (pos >= str.Length)
                    break;
            }

            sb.Append(str[pos]);
            pos++;
        }

        return sb.ToString();
    }

    private object ReadValue(string str, ref int pos)
    {
        if (str[pos] == '"')
        {
            pos++;
            var ret = ReadString(str, '"', ref pos);
            pos++;
            return ret;
        }

        if (str[pos] == '{')
        {
            pos++;
            var data = new ResultData();
            ReadTuple(str, ref pos, data);
            return data;
        }

        if (str[pos] == '[')
        {
            pos++;
            return ReadArray(str, ref pos);
        }

        // Single value tuple
        string name;
        object val;
        ReadResult(str, ref pos, out name, out val);
        var sdata = new ResultData();
        sdata._props = new Hashtable();
        sdata._props[name] = val;
        return sdata;
    }

    private void ReadTuple(string str, ref int pos, ResultData data)
    {
        if (data._props == null)
            data._props = new Hashtable();

        while (pos < str.Length && str[pos] != '}')
        {
            string name;
            object val;
            ReadResult(str, ref pos, out name, out val);
            if (data._props.ContainsKey(name))
            {
                var ob = data._props[name];
                var rd = ob as ResultData;
                if (rd != null && rd._isArrayProperty)
                {
                    var newArr = new object[rd._array.Length + 1];
                    Array.Copy(rd._array, newArr, rd._array.Length);
                    newArr[rd._array.Length] = val;
                    rd._array = newArr;
                }
                else
                {
                    rd = new ResultData();
                    rd._isArrayProperty = true;
                    rd._array = new object[2];
                    rd._array[0] = ob;
                    rd._array[1] = val;
                    data._props[name] = rd;
                }
            }
            else
            {
                data._props[name] = val;
            }

            TryReadChar(str, ref pos, ',');
        }

        TryReadChar(str, ref pos, '}');
    }

    private ResultData ReadArray(string str, ref int pos)
    {
        var list = new ArrayList();
        while (pos < str.Length && str[pos] != ']')
        {
            var val = ReadValue(str, ref pos);
            list.Add(val);
            TryReadChar(str, ref pos, ',');
        }

        TryReadChar(str, ref pos, ']');
        var arr = new ResultData();
        arr._array = list.ToArray();
        return arr;
    }

    private void ReadChar(string str, ref int pos, char c)
    {
        if (!TryReadChar(str, ref pos, c))
            ThrownParseError(str, pos);
    }

    private bool TryReadChar(string str, ref int pos, char c)
    {
        if (pos >= str.Length || str[pos] != c)
            return false;
        pos++;
        return true;
    }

    private void ThrownParseError(string str, int pos)
    {
        if (pos > str.Length)
            pos = str.Length;
        str = str.Insert(pos, "[!]");
        throw new InvalidOperationException("Error parsing result: " + str);
    }
}