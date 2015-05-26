﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;
using Examine;
using Examine.LuceneEngine;
using Examine.Providers;
using Examine.SearchCriteria;
using Microsoft.Owin.Testing;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Umbraco.Core.Configuration;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.PropertyEditors;
using Umbraco.Core.Services;
using Umbraco.RestApi.Routing;
using Umbraco.RestApi.Tests.TestHelpers;

namespace Umbraco.RestApi.Tests
{
    [TestFixture]
    public class MediaControllerTests
    {
        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            ConfigurationManager.AppSettings.Set("umbracoPath", "~/umbraco");
            ConfigurationManager.AppSettings.Set("umbracoConfigurationStatus", UmbracoVersion.Current.ToString(3));
            var mockSettings = MockUmbracoSettings.GenerateMockSettings();
            UmbracoConfig.For.CallMethod("SetUmbracoSettings", mockSettings);
        }

        [TearDown]
        public void TearDown()
        {
            //Hack - because Reset is internal
            typeof(PropertyEditorResolver).CallStaticMethod("Reset", true);
        }

        [Test]
        public async void Get_Root_With_OPTIONS()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    var mockMediaService = Mock.Get(serviceContext.MediaService);
                    mockMediaService.Setup(x => x.GetRootMedia()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedMedia(123, -1),
                        ModelMocks.SimpleMockedMedia(456, -1)
                    });

                    mockMediaService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedMedia(789, 123) });
                    mockMediaService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedMedia(321, 456) });
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {

                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Options,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Headers.Add("Access-Control-Request-Headers", "accept, authorization");
                request.Headers.Add("Access-Control-Request-Method", "GET");
                request.Headers.Add("Origin", "http://localhost:12061");
                request.Headers.Add("Referer", "http://localhost:12061/browser.html");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async void Get_Root_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services,
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    var mockMediaService = Mock.Get(serviceContext.MediaService);
                    mockMediaService.Setup(x => x.GetRootMedia()).Returns(new[]
                    {
                        ModelMocks.SimpleMockedMedia(123, -1),
                        ModelMocks.SimpleMockedMedia(456, -1)
                    });

                    mockMediaService.Setup(x => x.GetChildren(123)).Returns(new[] { ModelMocks.SimpleMockedMedia(789, 123) });
                    mockMediaService.Setup(x => x.GetChildren(456)).Returns(new[] { ModelMocks.SimpleMockedMedia(321, 456) });
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {

                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                var asdf = GlobalConfiguration.Configuration;

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual("/umbraco/rest/v1/media", djson["_links"]["root"]["href"].Value<string>());
                Assert.AreEqual(2, djson["totalResults"].Value<int>());
                Assert.AreEqual(2, djson["_links"]["content"].Count());
                Assert.AreEqual(2, djson["_embedded"]["content"].Count());
            }
        }

        [Test]
        public async void Search_200_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    var mockSearchResults = new Mock<ISearchResults>();
                    mockSearchResults.Setup(results => results.TotalItemCount).Returns(10);
                    mockSearchResults.Setup(results => results.Skip(It.IsAny<int>())).Returns(new[]
                    {
                        new SearchResult() {Id = 789},
                        new SearchResult() {Id = 456},
                    });

                    var mockSearchProvider = Mock.Get(searchProvider);
                    mockSearchProvider.Setup(x => x.CreateSearchCriteria()).Returns(Mock.Of<ISearchCriteria>());
                    mockSearchProvider.Setup(x => x.Search(It.IsAny<ISearchCriteria>(), It.IsAny<int>()))
                        .Returns(mockSearchResults.Object);

                    var mockMediaService = Mock.Get(serviceContext.MediaService);
                    mockMediaService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
                        .Returns(new[]
                        {
                            ModelMocks.SimpleMockedMedia(789),
                            ModelMocks.SimpleMockedMedia(456)
                        });
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/search?lucene=parentID:\\-1", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async void Get_Id_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                 (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                 {
                     var mockMediaService = Mock.Get(serviceContext.MediaService);

                     mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());

                     mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));

                     mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                 });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/123", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual("/umbraco/rest/v1/media/123", djson["_links"]["self"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/media/456", djson["_links"]["parent"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/media/{id}/children{?pageIndex,pageSize}", djson["_links"]["children"]["href"].Value<string>());
                Assert.AreEqual("/umbraco/rest/v1/media", djson["_links"]["root"]["href"].Value<string>());

                var properties = djson["properties"].ToObject<IDictionary<string, object>>();
                Assert.AreEqual(2, properties.Count());
                Assert.IsTrue(properties.ContainsKey("TestProperty1"));
                Assert.IsTrue(properties.ContainsKey("testProperty2"));
            }
        }

        [Test]
        public async void Get_Metadata_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                 (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                 {
                     var mockMediaService = Mock.Get(serviceContext.MediaService);

                     mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());
                     mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));
                     mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);

                     var mockTextService = Mock.Get(serviceContext.TextService);

                     mockTextService.Setup(x => x.Localize(It.IsAny<string>(), It.IsAny<CultureInfo>(), It.IsAny<IDictionary<string, string>>()))
                         .Returns((string input, CultureInfo culture, IDictionary<string, string> tokens) => input);
                 });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/123/meta", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                //TODO: Assert values!


            }
        }

        [Test]
        public async void Get_Children_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    var mockMediaService = Mock.Get(serviceContext.MediaService);

                    mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());

                    mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));

                    mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/123/children", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [Test]
        public async void Get_Children_Is_With_Params_Result()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    var mockMediaService = Mock.Get(serviceContext.MediaService);

                    mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());

                    long total = 6;
                    mockMediaService.Setup(x => x.GetPagedChildren(It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total, It.IsAny<string>(), Direction.Ascending, It.IsAny<string>()))
                        .Returns(new List<IMedia>(new[]
                        {
                            ModelMocks.SimpleMockedMedia(789),
                            ModelMocks.SimpleMockedMedia(456)
                        }));

                    mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/123/children?pageIndex=1&pageSize=2", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Get,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(6, djson["totalResults"].Value<int>());
                Assert.AreEqual(1, djson["pageIndex"].Value<int>());
                Assert.AreEqual(2, djson["pageSize"].Value<int>());
                Assert.IsNotNull(djson["_links"]["next"]);
                Assert.IsNotNull(djson["_links"]["prev"]);

            }
        }

        [Test]
        public async void Post_Is_201_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    SetupMocksForPost(serviceContext);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Content = new StringContent(@"{
  ""contentTypeAlias"": ""testType"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""name"": ""Home"",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [Test]
        public async void Post_Is_400_Validation_Required_Fields()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    SetupMocksForPost(serviceContext);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
  ""contentTypeAlias"": """",
  ""parentId"": 456,
  ""templateId"": 9,
  ""name"": """",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(2, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.contentTypeAlias", djson["_embedded"]["errors"][0]["logRef"].Value<string>());
                Assert.AreEqual("content.name", djson["_embedded"]["errors"][1]["logRef"].Value<string>());

            }
        }

        [Test]
        public async void Post_Is_400_Validation_Property_Missing()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    SetupMocksForPost(serviceContext);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
    ""name"": ""test"",  
    ""contentTypeAlias"": ""test"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""properties"": {
    ""thisDoesntExist"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(1, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.properties.thisDoesntExist", djson["_embedded"]["errors"][0]["logRef"].Value<string>());

            }
        }

        [Test]
        public async void Post_Is_400_Validation_Property_Required()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    SetupMocksForPost(serviceContext);

                    var mockPropertyEditor = Mock.Get(PropertyEditorResolver.Current);
                    mockPropertyEditor.Setup(x => x.GetByAlias("testEditor")).Returns(new ModelMocks.SimplePropertyEditor());
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Post,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                //NOTE: it is missing
                request.Content = new StringContent(@"{
    ""name"": ""test"",  
    ""contentTypeAlias"": ""test"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""properties"": {
    ""TestProperty1"": """",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);

                var djson = JsonConvert.DeserializeObject<JObject>(json);

                Assert.AreEqual(1, djson["totalResults"].Value<int>());
                Assert.AreEqual("content.properties.TestProperty1.value", djson["_embedded"]["errors"][0]["logRef"].Value<string>());

            }
        }

        [Test]
        public async void Put_Is_200_Response()
        {
            var startup = new TestStartup(
                //This will be invoked before the controller is created so we can modify these mocked services
                (request, umbCtx, typedContent, serviceContext, searchProvider) =>
                {
                    SetupMocksForPost(serviceContext);
                });

            using (var server = TestServer.Create(builder => startup.Configuration(builder)))
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(string.Format("http://testserver/umbraco/rest/v1/{0}/123", RouteConstants.MediaSegment)),
                    Method = HttpMethod.Put,
                };

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/hal+json"));
                request.Content = new StringContent(@"{
  ""contentTypeAlias"": ""testType"",
  ""parentId"": 456,
  ""templateId"": 9,
  ""name"": ""Home"",
  ""properties"": {
    ""TestProperty1"": ""property value1"",
    ""testProperty2"": ""property value2""
  }
}", Encoding.UTF8, "application/json");

                Console.WriteLine(request);
                var result = await server.HttpClient.SendAsync(request);
                Console.WriteLine(result);

                var json = await ((StreamContent)result.Content).ReadAsStringAsync();
                Console.Write(JsonConvert.SerializeObject(JsonConvert.DeserializeObject(json), Formatting.Indented));

                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        private void SetupMocksForPost(ServiceContext serviceContext)
        {
            var mockMediaService = Mock.Get(serviceContext.MediaService);
            mockMediaService.Setup(x => x.GetById(It.IsAny<int>())).Returns(() => ModelMocks.SimpleMockedMedia());
            mockMediaService.Setup(x => x.GetChildren(It.IsAny<int>())).Returns(new List<IMedia>(new[] { ModelMocks.SimpleMockedMedia(789) }));
            mockMediaService.Setup(x => x.HasChildren(It.IsAny<int>())).Returns(true);
            mockMediaService.Setup(x => x.CreateMedia(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()))
                .Returns(() => ModelMocks.SimpleMockedMedia(8888));

            var mockContentTypeService = Mock.Get(serviceContext.ContentTypeService);
            mockContentTypeService.Setup(x => x.GetContentType(It.IsAny<string>())).Returns(ModelMocks.SimpleMockedContentType());

            var mockDataTypeService = Mock.Get(serviceContext.DataTypeService);
            mockDataTypeService.Setup(x => x.GetPreValuesCollectionByDataTypeId(It.IsAny<int>())).Returns(new PreValueCollection(Enumerable.Empty<PreValue>()));

            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(x => x.GetService(It.IsAny<Type>())).Returns(new ModelMocks.SimplePropertyEditor());

            Func<IEnumerable<Type>> producerList = Enumerable.Empty<Type>;
            var mockPropertyEditorResolver = new Mock<PropertyEditorResolver>(
                Mock.Of<IServiceProvider>(),
                Mock.Of<ILogger>(),
                producerList);

            mockPropertyEditorResolver.Setup(x => x.PropertyEditors).Returns(new[] { new ModelMocks.SimplePropertyEditor() });

            PropertyEditorResolver.Current = mockPropertyEditorResolver.Object;
        }
    }

}