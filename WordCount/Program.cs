using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Web.Script.Serialization;

namespace WordCount
{
    internal class Program
    {
       
        public static string wordsCount(string inputFile)
        {
            
            StreamReader sr = new StreamReader(inputFile);
            string[] words = sr.ReadToEnd().Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string resultFilePath;
            int sumWords = words.Length;
            Dictionary<string, int> countWords = new Dictionary<string, int>();
            foreach (string word in words)
            {
                if(countWords.ContainsKey(word))
                {
                    countWords[word]++;
                }
                else
                {
                    countWords.Add(word, 1);
                }
            }
            var data = new
            {
                CountWords = countWords,
                SumWords = sumWords
            };
            resultFilePath = Path.ChangeExtension(inputFile, ".json");
            string countWordJson = JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
            Console.WriteLine( countWordJson);
            File.WriteAllText(resultFilePath, countWordJson);
            sr.Close();
           return resultFilePath;
        }
        static async Task<bool> BucketExists(IAmazonS3 client, string bucketName)
        {
            try
            {
                var response = await client.ListBucketsAsync();
                return response.Buckets.Exists(b => string.Equals(b.BucketName, bucketName));
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error checking bucket existence: '{ex.Message}'");
                return false;
            }
        }
        public static async Task<bool> CreateBucket(IAmazonS3 client, string bucketName)
        {
          
            try
            {
                var request = new PutBucketRequest
                {
                    BucketName = bucketName,
                    UseClientRegion = true,
                };
              
                var response = await client.PutBucketAsync(request);
                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error creating bucket: '{ex.Message}'");
                return false;
            }
        }

        public static async Task<bool> UploadFileAsync(
          IAmazonS3 client,
          string bucketName,
          string objectName,
          string filePath)
        {
            string countWordsFile = wordsCount(filePath);
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
                FilePath = filePath,
            };
            
            var sumFileRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = Path.GetFileName(countWordsFile),
                FilePath = countWordsFile,
            };
            var response = await client.PutObjectAsync(request);
            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                Console.WriteLine($"Successfully uploaded {objectName} to {bucketName}.");
                response = await client.PutObjectAsync(sumFileRequest);
                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"Successfully uploaded {"sum"+objectName} to {bucketName}.");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Could not upload {objectName} to {bucketName}.");
                    return false;
                }
            }
            else
            {
                Console.WriteLine($"Could not upload {objectName} to {bucketName}.");
                return false;
            }
        }
        public static async Task<bool> DownloadObjectFromBucketAsync(
          IAmazonS3 client,
          string bucketName,
          string objectName,
          string filePath)
        {
            
            // Create a GetObject request
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
            };

            // Issue request and remember to dispose of the response
             GetObjectResponse response = await client.GetObjectAsync(request);

            try
            {
                // Save object to local file
                await response.WriteResponseStreamToFileAsync($"{filePath}\\{objectName}", true, CancellationToken.None);
                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error saving {objectName}: {ex.Message}");
                return false;
            }
        }
        public static async Task<bool> ManageBucketAndUploadFiles(string bucketName, IAmazonS3 client)
        {
            string filePath, objectName;
            
            bool bucketExists = await BucketExists(client, bucketName);
            if (!bucketExists) {
                var success = await CreateBucket(client, bucketName);
                if (!success) { return false; }
            }
            await Console.Out.WriteLineAsync("insert file paths for stop press 0");
            filePath = Console.ReadLine();
            objectName = Path.GetFileName(filePath);
            while (!string.Equals(filePath,'0'))
            {
                if(!await UploadFileAsync(client, bucketName, objectName, filePath))
                { return false; }
                filePath = Console.ReadLine();
                objectName = Path.GetFileName(filePath);
            }
            return true;
        }
        static async Task Main(string[] args)
        {
            string jsonContent = File.ReadAllText("../../jsconfig1.json");

            JavaScriptSerializer ser = new JavaScriptSerializer();
            var accessData = ser.Deserialize<Dictionary<string, string>>(jsonContent);

            string AccessKeyId = accessData["AccessKeyId"];
            string SecretAccessKey = accessData["SecretAccessKey"];

            IAmazonS3 client = new AmazonS3Client(AccessKeyId, SecretAccessKey, RegionEndpoint.USEast1);

            string bucketName = "word-count-interview-task";
            string destinationPath, fileName;
            await Console.Out.WriteLineAsync("To upload a file press 1\r\nTo display results for a file press 2 ");
            int option = int.Parse(Console.ReadLine());
            switch(option)
            {
                case 1: 
                    await ManageBucketAndUploadFiles(bucketName,client);
                    break;

                case 2:
                    await Console.Out.WriteLineAsync("Please enter the path where you want the result file to be saved.");
                    destinationPath = Console.ReadLine();
                    await Console.Out.WriteLineAsync("enter the file name");
                    fileName = Console.ReadLine();
                    await DownloadObjectFromBucketAsync(client,bucketName,fileName, destinationPath);
                    break;
                         
            };


        }
    }
}
