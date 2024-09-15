using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);
//want the dependency to live for as long as the application server is running.
builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService()); 
var app = builder.Build();

//built-in middleware
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1"));
//custom middleware
app.Use(async (context, next) => {
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished.");
});

var todos = new List<Todo>(); //storing in memory for now.
app.MapPost("/todos", (Todo task, ITaskService service) =>
{
service.AddTodo(task);
return TypedResults.Created("/todos/{id}", task);

})
.AddEndpointFilter(async (context, next) => {
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();
    if(taskArgument.DueDate <DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate),["Cannot have due date in the past."]);
    }

    if(taskArgument.IsCompleted )
    {
        errors.Add(nameof(Todo.IsCompleted),["Cannot add completed todo."]);
    }

    if(errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }
    return await next(context);
});


//reterive todos based on their ids.
//MapGet means that it responds to a get request on the server
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoByID(id);
    return targetTodo is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(targetTodo);
});

//get all the todos
app.MapGet("/todos", (ITaskService service) => service.GetTodos());

//delete todos given an id.
app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});



app.Run();


public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted){}

interface ITaskService{
    Todo? GetTodoByID(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo task);
}

/*Since All of the todos that we manipulate in our application 
server are managed in memory, the calss InMemoryTaskService is decide.*/

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];
    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(task => id == task.Id);
    }

    public Todo? GetTodoByID(int id)
    {
        return _todos.SingleOrDefault(task => id == task.Id);
    }

    public List<Todo> GetTodos()
    {
        return _todos;
    }
}