using Umbraco.RestApi.Routing;
using WebApi.Hal;

namespace Umbraco.RestApi.Links
{
    public class PublishedContentLinkTemplate : IContentLinkTemplate
    {
        private readonly int _version;

        public PublishedContentLinkTemplate(int version)
        {
            _version = version;
        }

        public Link Root
        {
            get { return new Link("root", string.Format("~/{0}/{1}/{2}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link Self
        {
            get { return new Link("content", string.Format("~/{0}/{1}/{2}/{{id}}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link Parent
        {
            get { return new Link("parent", string.Format("~/{0}/{1}/{2}/{{parentId}}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link PagedDescendants
        {
            get { return new Link("descendants", string.Format("~/{0}/{1}/{2}/{{id}}/descendants{{?pageIndex,pageSize}}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link PagedChildren
        {
            get { return new Link("children", string.Format("~/{0}/{1}/{2}/{{id}}/children{{?pageIndex,pageSize}}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link MetaData
        {
            get { return new Link("meta", string.Format("~/{0}/{1}/{2}/{{id}}/meta", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }

        public Link Search
        {
            get { return new Link("search", string.Format("~/{0}/{1}/{2}/search{{?pageIndex,pageSize,lucene}}", RouteConstants.GetRestRootPath(_version), RouteConstants.ContentSegment, RouteConstants.PublishedSegment)); }
        }
    }
}