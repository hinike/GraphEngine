using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Trinity;
using Trinity.Storage;

namespace issue15
{
    public class issue15
    {
        [Fact]
        public void StorageEnumerationInterruption()
        {
            Enumerable.Range(0, 1024).ToList().ForEach(_ => Global.LocalStorage.SaveMyCell(_, _));
            int cnt = 0;
            foreach(var cell in Global.LocalStorage.MyCell_Selector())
            {
                ++cnt;
                if(cell.CellID == 256)
                {
                    Global.LocalStorage.RemoveCell(cell.CellID+1);
                }
                if(cell.CellID == 233)
                {
                    Global.LocalStorage.RemoveCell(cell.CellID+1);
                }
            }
            Assert.AreEqual(cnt, 1022);
        }
    }
}
