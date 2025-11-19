using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WitchyFormats;

namespace JortPob.Common
{
    public class SillyJsonUtils
    {
        public static void CopyRowAndModify(Paramanager paramanager, Paramanager.ParamType paramType, string name, int sourceRow, int destRow, Dictionary<string, string> data)
        {
            FsParam param = paramanager.param[paramType];
            FsParam.Row row = paramanager.CloneRow(param[sourceRow], name, destRow);

            foreach(KeyValuePair<string, string> property in data)
            {
                FsParam.Cell cell = (FsParam.Cell)row[property.Key];

                switch(cell.Value.GetType())
                {
                    case Type t when t == typeof(int):
                        cell.SetValue(int.Parse(property.Value));
                        break;
                    case Type t when t == typeof(uint):
                        cell.SetValue(uint.Parse(property.Value));
                        break;
                    case Type t when t == typeof(ushort):
                        cell.SetValue(ushort.Parse(property.Value));
                        break;
                    case Type t when t == typeof(short):
                        cell.SetValue(short.Parse(property.Value));
                        break;
                    case Type t when t == typeof(byte):
                        cell.SetValue(byte.Parse(property.Value));
                        break;
                    case Type t when t == typeof(sbyte):
                        cell.SetValue(sbyte.Parse(property.Value));
                        break;
                    case Type t when t == typeof(float):
                        cell.SetValue(float.Parse(property.Value));
                        break;
                    case Type t when t == typeof(bool):
                        cell.SetValue(bool.Parse(property.Value));
                        break;
                    case Type t when t == typeof(string):
                        cell.SetValue(property.Value);
                        break;
                    default:
                        throw new NotImplementedException($"Type {cell.Value.GetType()} not implemented in CopyRowAndModify.");
                }
            }

            paramanager.AddRow(param, row);
        }
    }
}
