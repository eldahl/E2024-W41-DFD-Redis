using BloggingPlatformAssignment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace BloggingPlatformAssignment;

[ApiController]
[Route("api/[controller]")]
public class BlogController : Controller
{
    private readonly MongoDBContext _dbContext;
    private readonly RedisClient _redisClient;

    public BlogController(MongoDBContext dbContext, RedisClient redisClient)
    {
        _redisClient = redisClient;
        _dbContext = dbContext;
    }


    [HttpGet("blogs")]
    public IActionResult GetBlogs()
    {
        var result = _dbContext.Collection<Blog>().Find(_ => true).ToList();

        return Ok(result);
    }

    [HttpGet("GetPostsFromBlogId")]
    public IActionResult GetPostsFromBlogId([FromQuery] Guid blogId)
    {
        var result = _dbContext.Collection<Post>().Find(x => x.BlogId.Equals(blogId)).ToList();
        return Ok(result);
    }

    [HttpGet("GetCommentsFromPost")]
    public IActionResult GetCommentsFromPost([FromQuery] Guid postId)
    {
        var result = _dbContext.Collection<Comment>().Find(x => x.PostId.Equals(postId)).ToList();
        return Ok(result);
    }

    [HttpPut("UpdatePost")]
    public IActionResult UpdatePost([FromBody] Post post)
    {
        var result = _dbContext.Collection<Post>().ReplaceOne(filter => filter.Id == post.Id, post);
        return Ok(result);
    }

    [HttpPut("UpdateUsername")]
    public IActionResult UpdateUsername([FromBody] UpdateUsernameDTO updateUser)
    {
        var newUser = _dbContext.Collection<User>().Find(x => x.Id.Equals(updateUser.UserId)).First();
        newUser.Username = updateUser.Username;
        var result = _dbContext.Collection<User>().ReplaceOne(filter => filter.Id == updateUser.UserId, newUser);
        return Ok(result);
    }

    [HttpGet("CreatePost")]
    public IActionResult CreatePost()
    {
        Post post = new Post();
        _redisClient.AddCacheOfPostIds(post);
        return Ok(post);
    }

    [HttpGet("GetList")]
    public IActionResult GetList()
    {
        var returnObj = _redisClient.GetCachedPostIds().ToList();
        return Ok(returnObj);
    }

    [HttpPost("CreateComment")]
    public IActionResult CreateComment([FromBody] Comment comment)
    {
        var timeOfLastComment = _redisClient.CheckPostCreationTime(comment.UserId);
        if (timeOfLastComment == null)
        {
            _dbContext.Collection<Comment>().InsertOne(comment);
            _redisClient.CommentsToCache(comment.UserId, DateTime.Now);
            return Ok(comment);
        }
        var subTract = DateTime.Now.Subtract(timeOfLastComment.Value);
        
        
        if (subTract.TotalSeconds >= 5)
        {
            _dbContext.Collection<Comment>().InsertOne(comment);
            _redisClient.CommentsToCache(comment.UserId, DateTime.Now);
            return Ok(comment);
        }

        return BadRequest("Is this post Spam? You posted a comment too quick.");
    }
}