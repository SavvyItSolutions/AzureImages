 using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Drawing.Drawing2D;
using System.Web;

namespace UpdateWines
{
    public class Program
    {
        int delta = 2;
        public static void Main(string[] args)
        {
            List<WineDetails> WineList = new List<WineDetails>();
            string str = ConfigurationManager.ConnectionStrings["DBConnection"].ConnectionString;
            using (SqlConnection con = new SqlConnection(str))
            {
                using (SqlCommand cmd = new SqlCommand("CheckMaxWineId", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Connection = con;
                    con.Open();
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    da.Fill(ds);
                    if (ds != null && ds.Tables.Count > 0)
                    {
                        if (ds.Tables[0].Rows.Count > 0)
                        {
                            foreach (DataRow dr in ds.Tables[0].Rows)
                            {
                                WineDetails WineObj = new WineDetails();
                                WineObj.WineId = Convert.ToInt32(dr["WineId"]);
                                WineObj.WineName = dr["WineName"].ToString();
                                WineObj.Vintage = dr["Vintage"].ToString();
                                WineList.Add(WineObj);
                            }
                            con.Close();
                        }
                    }
                }

            }
            Program p = new Program();
            int success = 0;
            foreach (WineDetails obj in WineList)
            {
                Image img = p.GetFile(obj.WineName, obj.Vintage);
                success = p.UploadImage(img, obj.WineId);

            }

            if (WineList.Count > 0)
            {
                int lastWineId = WineList[WineList.Count - 1].WineId;
                using (SqlConnection con = new SqlConnection(str))
                {
                    using (SqlCommand cmd = new SqlCommand("update updateWine set MaxWineID=@wineId", con))
                    {
                        cmd.Parameters.AddWithValue("@wineId", lastWineId);
                        cmd.Connection = con;
                        con.Open();
                        cmd.ExecuteNonQuery();
                        con.Close();
                    }

                }
            }

            p.getImagesFromDrive();          

        }


        private string GetHtmlCode(string wineName, string Vintage)
        {
            string searchText = "bottle image for " + wineName + " " + Vintage;

            string url = "https://www.google.com/search?q=" + searchText + "&tbm=isch";
            string data = "";

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Accept = "text/html, application/xhtml+xml, */*";
            request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";

            var response = (HttpWebResponse)request.GetResponse();

            using (Stream dataStream = response.GetResponseStream())
            {
                if (dataStream == null)
                    return "";
                using (var sr = new StreamReader(dataStream))
                {
                    data = sr.ReadToEnd();
                }
            }
            return data;
        }

        private List<string> GetUrls(string html)
        {
            var urls = new List<string>();

            int ndx = html.IndexOf("\"ou\"", StringComparison.Ordinal);

            int count = 0;
            while (ndx >= 0 && count < 10)
            {
                ndx = html.IndexOf("\"", ndx + 4, StringComparison.Ordinal);
                ndx++;
                int ndx2 = html.IndexOf("\"", ndx, StringComparison.Ordinal);
                string url = html.Substring(ndx, ndx2 - ndx);
                urls.Add(url);
                ndx = html.IndexOf("\"ou\"", ndx2, StringComparison.Ordinal);
                count++;
            }
            return urls;
        }

        private byte[] GetImage(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                var response = (HttpWebResponse)request.GetResponse();

                using (Stream dataStream = response.GetResponseStream())
                {
                    if (dataStream == null)
                        return null;
                    using (var sr = new BinaryReader(dataStream))
                    {
                        byte[] bytes = sr.ReadBytes(100000000);

                        return bytes;
                    }
                }

            }
            catch (Exception)
            {
                //throw;
            }
            return null;
        }

        public Image GetFile(string wineName, string Vintage)
        {
            string html = GetHtmlCode(wineName, Vintage);
            List<string> urls = GetUrls(html);
            Image img;
            Bitmap bitmp = null;
            for (int i = 0; i < urls.Count; i++)
            {
                string luckyUrl = urls[i];

                byte[] image = GetImage(luckyUrl);
                if (image == null)
                    continue;
                try
                {
                    using (var ms = new MemoryStream(image))
                    {
                        img = Image.FromStream(ms);
                    }
                    Bitmap x = img.Clone() as Bitmap;
                    int ret = possibleCandidate(x);
                    if (ret == 1)
                    {
                        return img;
                    }
                    else if (ret == 2 && bitmp == null)
                    {
                        //bitmp = MakeTransparent(x);
                        Bitmap newBitmap = new Bitmap(x.Width, x.Height);
                        for (int i1 = 0; i1 < x.Width; i1++)
                        {
                            for (int j = 0; j < x.Height; j++)
                            {
                                newBitmap.SetPixel(i1, j, x.GetPixel(i1, j));
                            }
                        }
                        bitmp = newBitmap;
                    }
                }
                catch (Exception ex)
                {
                    //string message = string.Format("Time: {0}", DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt"));
                    //string path = Server.MapPath("~/ErrorLog.txt");
                    //using (StreamWriter writer = new StreamWriter(path, true))
                    //{
                    //    writer.WriteLine(message);
                    //    writer.Close();
                    //}
                }
            }
            return (Image)bitmp;
        }

        private int possibleCandidate(Bitmap scrBitmap)
        {
            Color actualColor;
            //for (int i = 0; i < scrBitmap.Width; i++)
            //{
            //for (int j = 0; j < scrBitmap.Height; j++)
            //{
            //if (scrBitmap.Height / scrBitmap.Width < 3)
            //    return 0;

            actualColor = scrBitmap.GetPixel(0, 0);
            if (actualColor.A == 0 && actualColor.R == 0 && actualColor.G == 0 && actualColor.B == 0)
            {
                return 1;
            }
            if (MatchColor(actualColor))
            {
                return 2;
            }
            //}
            //}

            return 0;
        }

        private Bitmap MakeTransparent(Bitmap scrBitmap)
        {
            //You can change your new color here. Red,Green,LawnGreen any..
            Color actualColor;
            //make an empty bitmap the same size as scrBitmap
            Bitmap newBitmap = new Bitmap(scrBitmap.Width, scrBitmap.Height);
            for (int i = 0; i < scrBitmap.Width; i++)
            {
                for (int j = 0; j < scrBitmap.Height; j++)
                {
                    newBitmap.SetPixel(i, j, scrBitmap.GetPixel(i, j));
                }
            }

            for (int j = 0; j < scrBitmap.Height; j++)
            {
                for (int i = 0; i < scrBitmap.Width; i++)
                {
                    actualColor = scrBitmap.GetPixel(i, j);

                    if (MatchColor(actualColor))
                        newBitmap.SetPixel(i, j, Color.Transparent);
                    else
                        break; //newBitmap.SetPixel(i, j, actualColor);
                }
                for (int i = scrBitmap.Width - 1; i >= 0; i--)
                {
                    actualColor = scrBitmap.GetPixel(i, j);

                    if (MatchColor(actualColor))
                        newBitmap.SetPixel(i, j, Color.Transparent);
                    else
                        break; //newBitmap.SetPixel(i, j, actualColor);
                }
            }


            return newBitmap;
        }

        private bool MatchColor(Color actualColor)
        {
            if (255 - actualColor.A <= delta && 255 - actualColor.R <= delta && 255 - actualColor.G <= delta && 255 - actualColor.B <= delta)
                return true;
            else
                return false;
        }

        private int UploadImage(Image BottleImage, int WineId)
        {
            string conStrings = ConfigurationManager.ConnectionStrings["AzureStorageConnection"].ConnectionString;
            CloudStorageAccount storageaccount = CloudStorageAccount.Parse(conStrings);
            CloudBlobClient blobClient = storageaccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("bottleimages");
            container.CreateIfNotExists();
            //For BottleImages
            CloudBlockBlob blob = container.GetBlockBlobReference(WineId + ".jpg");
            if (BottleImage != null)
            {
                Image ImageForBottle = ResizeImage(BottleImage, BottleImage.Width, BottleImage.Height,250,300);
                string path = @"C:\soumik\personal\New folder\" + WineId + ".jpg";
                ImageForBottle.Save(path);

                using (var fs = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    blob.UploadFromStream(fs);
                    fs.Close();
                }

                File.Delete(path);

                //For BottleDetailsImages
                container = blobClient.GetContainerReference("bottleimagesdetails");
                blob = container.GetBlockBlobReference(WineId+".jpg");
                ImageForBottle = ResizeImage(BottleImage, BottleImage.Width, BottleImage.Height, 750, 900);
                ImageForBottle.Save(path);
                using (var fs = System.IO.File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    blob.UploadFromStream(fs);                                      
                    fs.Close();
                }
                File.Delete(path);
                //ImageForBottle.Dispose();
                BottleImage.Dispose();           
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public static Bitmap ResizeImage(Image image, int width, int height,int desiredWidth,int desiredHeight)
        {
            //float ratio = ((float)240) / height;
            //ratio = ratio / 2;
            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)desiredWidth / (float)width);
            nPercentH = ((float)desiredHeight / (float)height);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;
            float ratio = nPercent;
            var destRect = new Rectangle(0, 0, Convert.ToInt32(width * ratio), Convert.ToInt32(height * ratio));
            var destImage = new Bitmap(Convert.ToInt32(width * ratio), Convert.ToInt32(height * ratio));

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private void getImagesFromDrive()
        {
            string path = ConfigurationManager.AppSettings["GoogleDrivePath"];
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] Images = null; 
            bool IsPresent = di.GetFiles("*.jpg").Any();
            if(IsPresent)
            {
                 Images = di.GetFiles("*.jpg");
                 for(int i=0;i<Images.Length;i++)
                 {
                    string[] wineName = Images[i].Name.Split('.');
                    int sku = int.Parse(wineName[0]);
                    int wineId = getWineId(sku);
                    if (wineId > 0)
                    {
                        string fullPath = path + "\\" + Images[i].Name;
                        UploadImage(Image.FromFile(fullPath), wineId);
                        File.Delete(fullPath);
                    }
                 }
            }

            
        }

        private int getWineId(int sku)
        {
            int wineId = 0;
            string str = ConfigurationManager.ConnectionStrings["DBConnection"].ConnectionString;
            using (SqlConnection con = new SqlConnection(str))
            {
                using (SqlCommand cmd = new SqlCommand("GetWineIdForSKU", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@sku",sku);
                    cmd.Connection = con;
                    con.Open();
                    wineId = Convert.ToInt32(cmd.ExecuteScalar());
                }

            }
            return wineId;

        }
    }
}
