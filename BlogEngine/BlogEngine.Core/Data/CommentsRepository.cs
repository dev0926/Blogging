﻿using BlogEngine.Core.Data.Contracts;
using BlogEngine.Core.Data.Models;
using BlogEngine.Core.Data.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic;
using System.Web;
using System.Web.Security;

namespace BlogEngine.Core.Data
{
    /// <summary>
    /// Comments repository
    /// </summary>
    public class CommentsRepository : ICommentsRepository
    {
        /// <summary>
        /// Comments list
        /// </summary>
        /// <param name="commentType">Comment type</param>
        /// <param name="take">Items to take</param>
        /// <param name="skip">Items to skip</param>
        /// <param name="filter">Filter expression</param>
        /// <param name="order">Sort order</param>
        /// <returns>List of comments</returns>
        public IEnumerable<CommentItem> GetComments(CommentType commentType = CommentType.All, int take = 10, int skip = 0, string filter = "", string order = "")
        {
            if (!Security.IsAuthorizedTo(Rights.ViewPublicComments))
                throw new UnauthorizedAccessException();

            if (string.IsNullOrEmpty(filter)) filter = "1==1";
            if (string.IsNullOrEmpty(order)) order = "DateCreated desc";

            var items = new List<Comment>();
            var query = items.AsQueryable().Where(filter);
            var comments = new List<CommentItem>();

            var all = Security.IsAuthorizedTo(Rights.EditOtherUsersPosts);

            foreach (var p in Post.Posts)
            {
                if (all || p.Author.ToLower() == Security.CurrentUser.Identity.Name.ToLower())
                {
                    switch (commentType)
                    {
                        case CommentType.Pending:
                            items.AddRange(p.NotApprovedComments);
                            break;
                        case CommentType.Pingback:
                            items.AddRange(p.Pingbacks);
                            break;
                        case CommentType.Spam:
                            items.AddRange(p.SpamComments);
                            break;
                        case CommentType.Approved:
                            items.AddRange(p.ApprovedComments);
                            break;
                        default:
                            items.AddRange(p.Comments);
                            break;
                    }
                }
            }

            // if take passed in as 0, return all
            if (take == 0) take = items.Count;        

            var itemList = query.OrderBy(order).Skip(skip).Take(take).ToList();

            foreach (var item in itemList)
            {
                comments.Add(Json.GetComment(item, itemList));               
            }

            return comments;
        }

        /// <summary>
        /// Single commnet by ID
        /// </summary>
        /// <param name="id">
        /// Comment id
        /// </param>
        /// <returns>
        /// A JSON Comment
        /// </returns>
        public CommentItem FindById(Guid id)
        {
            if (!Security.IsAuthorizedTo(Rights.ViewPublicComments))
                throw new UnauthorizedAccessException();

            return (from p in Post.Posts
                    from c in p.AllComments
                    where c.Id == id
                    select Json.GetComment(c, p.AllComments)).FirstOrDefault();
        }

        /// <summary>
        /// Add item
        /// </summary>
        /// <param name="item">Comment</param>
        /// <returns>Comment object</returns>
        public CommentItem Add(CommentItem item)
        {
            if (!Security.IsAuthorizedTo(Rights.CreateComments))
                throw new UnauthorizedAccessException();

            var c = new Comment();
            try
            {
                var post = Post.Posts.Where(p => p.Id == item.PostId).FirstOrDefault();

                c.Id = Guid.NewGuid();
                c.ParentId = item.ParentId;
                c.IsApproved = item.IsApproved;
                c.Content = HttpUtility.HtmlAttributeEncode(item.Content);

                if (string.IsNullOrEmpty(item.Author))
                {
                    c.Author = Security.CurrentUser.Identity.Name;
                    var profile = AuthorProfile.GetProfile(c.Author);
                    if(profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                    {
                        c.Author = profile.DisplayName;
                    }
                }  

                if (string.IsNullOrEmpty(item.Email))
                    c.Email = Membership.Provider.GetUser(Security.CurrentUser.Identity.Name, true).Email;

                c.IP = Utils.GetClientIP();
                c.DateCreated = DateTime.Now;
                c.Parent = post;

                post.AddComment(c);
                post.Save();

                var newComm = post.Comments.Where(cm => cm.Content == c.Content).FirstOrDefault();

                return Json.GetComment(newComm, post.Comments);
            }
            catch (Exception ex)
            {
                Utils.Log("Core.Data.CommentsRepository.Add", ex);
                return null;
            }
        }

        /// <summary>
        /// Update item
        /// </summary>
        /// <param name="item">Item to update</param>
        /// <param name="action">Action</param>
        /// <returns>True on success</returns>
        public bool Update(CommentItem item, string action)
        {
            if (!Security.IsAuthorizedTo(Rights.ModerateComments))
                throw new UnauthorizedAccessException();

            foreach (var p in Post.Posts.ToArray())
            {
                foreach (var c in p.Comments.Where(c => c.Id == item.Id).ToArray())
                {
                    if (action == "approve")
                    {
                        c.IsApproved = true;
                        c.IsSpam = false;
                        p.DateModified = DateTime.Now;
                        p.Save();
                        return true;
                    }

                    if (action == "unapprove")
                    {
                        c.IsApproved = false;
                        c.IsSpam = true;
                        p.DateModified = DateTime.Now;
                        p.Save();
                        return true;
                    }

                    c.Content = item.Content;
                    c.Author = item.Author;
                    c.Email = item.Email;
                    c.Website = string.IsNullOrEmpty(item.Website) ? null : new Uri(item.Website);

                    if (item.IsPending)
                    {
                        c.IsApproved = false;
                        c.IsSpam = false;
                    }
                    if (item.IsApproved)
                    {
                        c.IsApproved = true;
                        c.IsSpam = false;
                    }
                    if (item.IsSpam)
                    {
                        c.IsApproved = false;
                        c.IsSpam = true;
                    }
                    // need to mark post as "dirty"
                    p.DateModified = DateTime.Now;
                    p.Save();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Delete item
        /// </summary>
        /// <param name="id">Item ID</param>
        /// <returns>True on success</returns>
        public bool Remove(Guid id)
        {
            if (!Security.IsAuthorizedTo(Rights.ModerateComments))
                throw new UnauthorizedAccessException();

            foreach (var p in Post.Posts.ToArray())
            {
                Comment item = (from cmn in p.AllComments
                    where cmn.Id == id select cmn).FirstOrDefault();

                if (item != null)
                {
                    p.RemoveComment(item);
                    p.DateModified = DateTime.Now;
                    p.Save();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Delete all comments
        /// </summary>
        /// <param name="commentType">Pending or spam</param>
        /// <returns>True on success</returns>
        public bool DeleteAll(string commentType)
        {
            if (!Security.IsAuthorizedTo(Rights.ModerateComments))
                throw new System.UnauthorizedAccessException();

            if (commentType == "pending")
                DeletePending();

            if (commentType == "spam")
                DeleteSpam();

            return true;
        }

        #region Private methods

        // delete all pending comments
        private void DeletePending()
        {
            var posts = Post.ApplicablePosts.Where(p => !p.IsDeleted && p.IsPublished);
            foreach (var p in posts.ToArray())
            {
                foreach (var c in p.NotApprovedComments.Where(c => !c.IsSpam && !c.IsDeleted))
                {
                    p.RemoveComment(c, false);
                }

                p.DateModified = DateTime.Now;
                p.Save();
            }
        }

        // delete all spam comments
        private void DeleteSpam()
        {
            var posts = Post.ApplicablePosts.Where(p => !p.IsDeleted && p.IsPublished);
            foreach (var p in posts.ToArray())
            {
                foreach (var c in p.SpamComments.Where(c => !c.IsDeleted))
                {
                    p.RemoveComment(c, false);
                }

                p.DateModified = DateTime.Now;
                p.Save();
            }
        }

        #endregion
    }
}