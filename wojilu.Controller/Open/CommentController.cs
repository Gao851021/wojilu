﻿using System;
using System.Collections.Generic;
using System.Text;
using wojilu.Web.Mvc;
using wojilu.Members.Users.Domain;
using wojilu.Common;
using wojilu.Web.Mvc.Attr;
using wojilu.Common.Comments;
using wojilu.Serialization;

namespace wojilu.Web.Controller.Open {



    public class CommentController : ControllerBase {

        public OpenCommentService commentService { get; set; }

        public CommentController() {
            commentService = new OpenCommentService();
        }


        public void List() {

            String url = ctx.Get( "url" );
            set( "thisUrl", url );

            DataPage<OpenComment> datas = commentService.GetByUrlDesc( url );

            List<OpenComment> lists = datas.Results;

            set( "cmCount", lists.Count );
            set( "moreLink", to( MoreReply ) );
            set( "subCacheSize", OpenComment.subCacheSize );

            bindComments( lists );
            bindForm();

            set( "page", datas.PageBar );
        }

        [HttpPost]
        public void MoreReply() {

            int parentId = ctx.PostInt( "parentId" );
            if (parentId <= 0) echoJson( "[]" );

            int startId = ctx.PostInt( "startId" );
            if (startId <= 0) echoJson( "[]" );

            List<OpenComment> moreList = commentService.GetMore( parentId, startId, OpenComment.subCacheSize, "desc" );
            List<CommentDto> dtoList = getCommentDto( moreList );
            echoJson( JsonString.ConvertList( dtoList ) );
        }

        private List<CommentDto> getCommentDto( List<OpenComment> moreList ) {

            List<CommentDto> list = new List<CommentDto>();
            foreach (OpenComment c in moreList) {

                Dictionary<String, String> userInfo = getUserInfo( c.Member, c.Author );

                CommentDto dto = new CommentDto();
                dto.Id = c.Id;
                dto.UserName = userInfo["userName"];
                dto.UserFace = userInfo["userFace"];
                dto.AuthorText = userInfo["authorText"];
                dto.Content = c.Content;
                dto.Created = c.Created.ToString( "g" );

                list.Add( dto );
            }

            return list;
        }

        private Dictionary<String, String> getUserInfo( User user, String authorName ) {

            Dictionary<String, String> dic = new Dictionary<String, String>();

            String userFace = "";
            String userName = "";
            String authorText = "";

            if (user != null && user.Id > 0) {
                userFace = string.Format( "<a href='{0}'><img src='{1}' style='width:48px;'/></a>", Link.ToMember( user ), user.PicSmall );
                userName = string.Format( "<a href='{0}'>{1}</a>", Link.ToMember( user ), user.Name );
                authorText = user.Name;
            }
            else {

                userFace = "<img src='" + sys.Path.AvatarGuest + "' style='width:48px;'/></a>";
                userName = authorName;
                authorText = authorName;
            }

            dic.Add( "userFace", userFace );
            dic.Add( "userName", userName );
            dic.Add( "authorText", authorText );

            return dic;
        }

        private void bindComments( List<OpenComment> lists ) {

            Boolean canAdmin = false;

            IBlock block = getBlock( "list" );
            foreach (OpenComment c in lists) {

                bindSingleInfo( canAdmin, block, c );
                List<OpenComment> subLists = c.GetReplyList();
                block.Set( "c.StartId", getStartId( subLists ) );

                IBlock subBlock = block.GetBlock( "replyList" );
                bindSubList( subBlock, c, subLists );

                block.Next();

            }
        }

        private int getStartId( List<OpenComment> lists ) {
            if (lists.Count == 0) return 0;
            return lists[lists.Count - 1].Id;
        }

        private void bindSubList( IBlock block, OpenComment comment, List<OpenComment> lists ) {

            Boolean canAdmin = false;

            foreach (OpenComment c in lists) {

                bindSingleInfo( canAdmin, block, c );

                block.Next();

            }
        }

        private void bindSingleInfo( Boolean canAdmin, IBlock block, OpenComment c ) {
            Dictionary<String, String> userInfo = getUserInfo( c.Member, c.Author );

            block.Set( "c.UserName", userInfo["userName"] );
            block.Set( "c.AuthorText", userInfo["authorText"] );
            block.Set( "c.UserFace", userInfo["userFace"] );

            block.Set( "c.Created", c.Created );
            block.Set( "c.Content", getContent( c ) );
            block.Set( "c.Id", c.Id );
            block.Set( "c.ParentId", c.ParentId );
            block.Set( "c.Replies", c.Replies );

            if (canAdmin) {
                IBlock adminBlock = block.GetBlock( "admin" );
                String deleteLink = to( Delete, c.Id );
                adminBlock.Set( "c.DeleteLink", deleteLink );
                adminBlock.Next();
            }
        }


        // TODO 罗列子列表
        private String getContent( OpenComment c ) {
            //if (c.ParentId == 0) return c.Content;
            //IComment parent = commentService.GetById( c.ParentId, ctx.app.Id );
            //if (parent == null) return c.Content;
            //String quote = "<div class='quote'><span class='qSpan'>{0} : {1}</span></div>";
            //return string.Format( quote, parent.Author, strUtil.CutString( parent.Content, 50 ) ) + "<div>" + c.Content + "</div>";

            return c.Content;
        }

        private void bindForm() {
            set( "createLink", to( Create ) );

            IBlock loginForm = getBlock( "loginForm" );
            IBlock guestForm = getBlock( "guestForm" );
            if (ctx.viewer.IsLogin) {
                loginForm.Next();
            }
            else {
                guestForm.Set( "Captcha", Html.Captcha );

                guestForm.Set( "contentLength", getContentTip() );
                guestForm.Next();
            }

            Dictionary<String, String> userInfo = getUserInfo( ctx.viewer.obj as User, "" ); // TODO 游客信息
            set( "userFace", userInfo["userFace"] );
            set( "userName", userInfo["userName"] );
        }

        private String getContentTip() {
            return string.Format( lang( "contentLength" ), config.Instance.Site.CommentLength );
        }

        [HttpPost]
        public void Create() {

            String userName;
            if (ctx.viewer.IsLogin) {
                userName = ctx.viewer.obj.Name;
            }
            else {
                userName = ctx.Post( "UserName" );
                if (strUtil.IsNullOrEmpty( userName )) errors.Add( lang( "exRequireAuthor" ) );
                if (strUtil.HasText( userName ) && userName.Length < 2) errors.Add( lang( "exAuthorShort" ) );
            }

            if (ctx.viewer.IsLogin == false) Html.Captcha.CheckError( ctx );

            String content = strUtil.CutString( ctx.Post( "Content" ), config.Instance.Site.CommentLength );
            if (strUtil.IsNullOrEmpty( content )) errors.Add( lang( "exRequireContent" ) );
            if (strUtil.HasText( content ) && content.Length < 3) errors.Add( lang( "exContentShort" ) );
            if (getContentTip().Equals( content )) errors.Add( lang( "exRequireContent" ) );

            if (ctx.HasErrors) {
                echoError();
                return;
            }

            OpenComment c = new OpenComment();
            c.Content = content;
            c.TargetUrl = ctx.Post( "url" );
            c.Ip = ctx.Ip;
            c.Author = userName;
            c.AuthorEmail = ctx.Post( "UserEmail" );
            c.ParentId = ctx.PostInt( "ParentId" );
            c.AtId = ctx.PostInt( "AtId" );

            if (ctx.viewer.IsLogin) {
                c.Member = (User)ctx.viewer.obj;
            }

            Result result = commentService.Create( c );

            if (result.IsValid) {
                echoAjaxOk();
            }
            else {
                echoError( result );
            }
        }

        public void Delete( int id ) {
        }

    }

}
