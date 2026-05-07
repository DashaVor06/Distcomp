using AutoMapper;
using BusinessLogic.DTO.Request;
using BusinessLogic.DTO.Response;
using DataAccess.Models;
using Infrastructure.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using BusinessLogic.Repositories;

public class CreatorService : BaseService<Creator, CreatorRequestTo, CreatorResponseTo>
{
    public CreatorService(IRepository<Creator> repository, IMapper mapper, IDistributedCache cache)
        : base(repository, mapper, cache)
    { }

    public async override Task<CreatorResponseTo> CreateAsync(CreatorRequestTo entityRequest)
    {
        // 1. Возвращаем проверку на существование (логика из старой версии)
        var allCreators = await _repository.GetAllAsync();
        if (allCreators.Any(c => c.Login == entityRequest.Login))
        {
            throw new BaseException(403, "Creator with such login already exists");
        }

        // 2. Ручной маппинг и создание (как было раньше)
        Creator creator = _mapper.Map<Creator>(entityRequest);
        creator.Id = await _repository.GetLastIdAsync() + 1;

        await _repository.CreateAsync(creator);

        // 3. Добавляем логику кэширования, чтобы данные не расходились
        var response = _mapper.Map<CreatorResponseTo>(creator);

        // Кэшируем новую запись по ID
        await SetCacheAsync(GetCacheKey(creator.Id), response);

        // Инвалидируем (сбрасываем) список всех креаторов в Redis
        await InvalidateAllCacheAsync();

        return response;
    }
}