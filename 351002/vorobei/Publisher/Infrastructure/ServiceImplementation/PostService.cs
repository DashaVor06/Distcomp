using Infrastructure.Kafka;
using BusinessLogic.DTO.Request;
using BusinessLogic.DTO.Response;
using BusinessLogic.Servicies;
using DataAccess.Models;

public class PostService : IBaseService<PostRequestTo, PostResponseTo>
{
    private readonly PostModerationProducer _kafkaProducer;
    private readonly IModerationResultWaiter _resultWaiter;

    public PostService(PostModerationProducer kafkaProducer, IModerationResultWaiter resultWaiter)
    {
        _kafkaProducer = kafkaProducer;
        _resultWaiter = resultWaiter;
    }

    // CREATE: Генерируем ID сами, отправляем в Kafka и СРАЗУ возвращаем ответ
    public async Task<PostResponseTo> CreateAsync(PostRequestTo entity)
    {
        entity.Id = Math.Abs(Guid.NewGuid().GetHashCode());
        entity.State = PostState.PENDING;

        // Отправка в InTopic (без ожидания результата обработки)
        await _kafkaProducer.SendAsync(entity.Id.ToString(), MessageType.Create, entity);

        return new PostResponseTo
        {
            Id = entity.Id,
            StoryId = entity.StoryId,
            Content = entity.Content,
            State = entity.State
        };
    }

    // UPDATE: Отправляем и ждем 1 секунду ответа в OutTopic
    public async Task<PostResponseTo?> UpdateAsync(PostRequestTo entity)
    {
        await _kafkaProducer.SendAsync(entity.Id.ToString(), MessageType.Update, entity);

        return await _resultWaiter.WaitForResultAsync(entity.Id, TimeSpan.FromSeconds(1));
    }

    // GET: Отправляем команду и ждем 1 секунду
    public async Task<PostResponseTo?> GetByIdAsync(int id)
    {
        await _kafkaProducer.SendAsync(id.ToString(), MessageType.Get, id);

        return await _resultWaiter.WaitForResultAsync(id, TimeSpan.FromSeconds(1));
    }

    // DELETE: Отправляем команду и ждем 1 секунду (подтверждение)
    public async Task<bool> DeleteByIdAsync(int id)
    {
        await _kafkaProducer.SendAsync(id.ToString(), MessageType.Delete, id);

        var result = await _resultWaiter.WaitForResultAsync(id, TimeSpan.FromSeconds(1));
        return result != null;
    }

    // GET ALL: Ждем 1 секунду список
    public async Task<List<PostResponseTo>> GetAllAsync()
    {
        const string queryKey = "all_query";
        await _kafkaProducer.SendAsync(queryKey, MessageType.GetAll, null);

        var results = await _resultWaiter.WaitForListResultAsync(queryKey, TimeSpan.FromSeconds(1));
        return results ?? new List<PostResponseTo>();
    }
}