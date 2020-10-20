using log4net.Repository;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddlewareRequestLogging.Middleware
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public RequestResponseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
            ConfigureLogging();
        }

        private void ConfigureLogging()
        {
            ILoggerRepository repo = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly());
            var fileInfo = new FileInfo(@"log4net.config");

            log4net.Config.XmlConfigurator.Configure(repo, fileInfo);
        }

        public async Task Invoke(HttpContext context)
        {
            //get incoming request
            var request = await FormatRequest(context.Request);

            //pointer to orig response body stream
            var originalBodyStream = context.Response.Body;

            //new mem stream
            using (var responseBody = new MemoryStream())
            {
                //gebruiken voor temp resp. body
                context.Response.Body = responseBody;

                //wachten tot verder in middleware om hier dan terug in te pikken
                await _next(context);

                //verwerken van de server response
                var response = await FormatResponse(context.Response);

                //TODO: opslaan van log naar data store
                _log.Info(request);
                _log.Info(response);

                //???
                //Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }


        private async Task<string> FormatRequest(HttpRequest request)
        {
            var body = request.Body;

            //maken dat request meer dan 1x kan gelezen worden
            request.EnableBuffering();

            //lezen req stream > byte array maken met zelfde lengte als req. stream
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];

            //kopiëren van request stream in nieuwe buffer
            await request.Body.ReadAsync(buffer, 0, buffer.Length);

            //byte array naar string met UTF8 encodering converteren
            var bodyAsText = Encoding.UTF8.GetString(buffer);

            //body assignen aan requustbody
            request.Body = body;

            return $"{request.Scheme} {request.Host}{request.Path}{request.QueryString} {bodyAsText}";
        }
        private async Task<string> FormatResponse(HttpResponse response)
        {
            //read response stream from beginning
            response.Body.Seek(0, SeekOrigin.Begin);

            //kopiëren naar string
            string text = await new StreamReader(response.Body).ReadToEndAsync();

            //reset reader zodat client kan lezen
            response.Body.Seek(0, SeekOrigin.Begin);

            //string voor response returnen, incl statuscode
            return $"{response.StatusCode}: {text}";
        }

    }
}
