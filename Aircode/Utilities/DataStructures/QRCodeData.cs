namespace Aircode.Utilities.DataStructures;
using Newtonsoft.Json;
public class QRCodeData
{
    public string? testData { get; set; }
    public float other { get; set; }
    public string? the { get; set; }

    public QRCodeData(string p1,float p2, string p3)
    {
        testData = p1;
        other = p2;
        the = p3;
    }

    public override string ToString()
    {
      string str =  JsonConvert.SerializeObject(this);
      return str;
    }
}