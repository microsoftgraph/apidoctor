﻿namespace OneDrive.ApiDocumentation.Validation.Http
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using System.IO;


    public class HttpResponse
    {
        public string HttpVersion { get; set; }
        public int StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public System.Net.WebHeaderCollection Headers { get; set; }

        public string Body { get; set; }

        public bool WasSuccessful { get { return StatusCode >= 200 && StatusCode < 300; } }

        public string ContentType { get { return Headers["content-type"]; } }

        public static async Task<HttpResponse> ResponseFromHttpWebResponseAsync(HttpWebRequest request)
        {
            HttpWebResponse webResponse;
            try
            {
                webResponse = (HttpWebResponse)await request.GetResponseAsync();
            }
            catch (WebException webex)
            {
                webResponse = webex.Response as HttpWebResponse;
                if (null == webResponse)
                {
                    return new HttpResponse {
                        StatusCode = 504,
                        StatusMessage = "HttpResponseFailure " + webex.Message
                    };
                }
            }
            catch (Exception ex)
            {
                return new HttpResponse {
                    StatusCode = 504,
                    StatusMessage = "HttpResponseFailure " + ex.Message
                };
            }

            return await ConvertToResponse(webResponse);
        }

        private static async Task<HttpResponse> ConvertToResponse(HttpWebResponse webresp)
        {
            HttpResponse resp;
            resp = new HttpResponse();
            resp.HttpVersion = string.Format("HTTP/{0}.{1}", webresp.ProtocolVersion.Major, webresp.ProtocolVersion.Minor);
            resp.StatusCode = (int)webresp.StatusCode;
            resp.StatusMessage = webresp.StatusDescription;

            resp.Headers = webresp.Headers;

            using (StreamReader reader = new StreamReader(webresp.GetResponseStream()))
            {
                resp.Body = await reader.ReadToEndAsync();
            }
            return resp;
        }

        public string FullHttpText()
        {
            return FormatFullResponse(Body);
        }

        public string FormatFullResponse(string body)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2}", HttpVersion, StatusCode, StatusMessage);
            sb.AppendLine();
            if (null != Headers)
            {
                foreach (var key in Headers.AllKeys)
                {
                    sb.AppendFormat("{0}: {1}", key, Headers[key]);
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
            
            sb.Append(body);

            return sb.ToString();
        }

        private readonly string[] HeadersForPartialMatch = { "content-type" };

        public bool CompareToResponse(HttpResponse actualResponse, out ValidationError[] errors)
        {
            List<ValidationError> errorList = new List<ValidationError>();
            if (StatusCode != actualResponse.StatusCode)
            {
                errorList.Add(new ValidationError(ValidationErrorCode.HttpStatusCodeDifferent, null, "Expected status code :{0}, received: {1}.", StatusCode, actualResponse.StatusCode));
            }

            if (StatusMessage != actualResponse.StatusMessage)
            {
                errorList.Add(new ValidationError(ValidationErrorCode.HttpStatusMessageDifferent, null, "Expected status message {0}, received: {1}.", StatusMessage, actualResponse.StatusMessage));
            }

            // Check to see that expected headers were found in the response
            List<string> otherResponseHeaderKeys = new List<string>();
            if (actualResponse.Headers != null)
            {
                otherResponseHeaderKeys.AddRange(actualResponse.Headers.AllKeys);
            }

            var comparer = new HeaderNameComparer();
            foreach(var expectedHeader in Headers.AllKeys)
            {
                if (!otherResponseHeaderKeys.Contains(expectedHeader, comparer))
                {
                    errorList.Add(new ValidationError(ValidationErrorCode.HttpRequiredHeaderMissing, null, "Response is missing header expected header: {0}.", expectedHeader));
                }
                else if (HeadersForPartialMatch.Contains(expectedHeader.ToLower()))
                {
                    var expectedValue = Headers[expectedHeader];
                    var actualValue = actualResponse.Headers[expectedHeader];

                    if (!actualValue.ToLower().StartsWith(expectedValue))
                    {
                        errorList.Add(new ValidationError(ValidationErrorCode.HttpHeaderValueDifferent, null, "Header '{0}' has unexpected value '{1}' (expected {2})", expectedHeader, actualValue, expectedValue));
                    }
                }
            }

            errors = errorList.ToArray();
            return errors.Length == 0;
        }

        class HeaderNameComparer : IEqualityComparer<string>
        {

            public bool Equals(string x, string y)
            {
                return String.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                if (null != obj) return obj.GetHashCode();
                return 0;
            }
        }

    }
}
