using AutoLot.Application.Chat;
using AutoLot.Application.Chat.Dtos;
using AutoLot.Application.Common.Abstractions;
using AutoLot.Application.Listings;
using AutoLot.Domain.Chat;
using AutoLot.Domain.Enums;
using AutoLot.Domain.Listings;
using AutoLot.Infrastructure.Listings;
using AutoLot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutoLot.Infrastructure.Chat;

/// <summary>
/// Приватне листування.
///
/// Хто чий співрозмовник, вирішується тут за даними з бази. Покупець
/// записаний у розмові, а продавцем виступає той, хто **може керувати
/// оголошенням** — те саме правило, що й для решти дій з ним. Завдяки цьому
/// менеджер салону відповідає на листи колеги, а не мовчить, бо «розмова
/// не його».
/// </summary>
internal sealed class ChatService(
    AutoLotDbContext dbContext,
    IDateTimeProvider clock,
    ListingAccess access,
    IChatNotifier notifier) : IChatService
{
    /// <summary>Оголошення, під якими можна листуватися.</summary>
    private static readonly ListingStatus[] Reachable =
        [ListingStatus.Active, ListingStatus.Sold];

    public async Task<IReadOnlyList<ConversationSummary>> GetMineAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var dealershipIds = await access.DealershipIdsOfAsync(userId, cancellationToken);

        var conversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(item =>
                item.BuyerId == userId
                || item.Listing.SellerId == userId
                || (item.Listing.DealershipId != null
                    && dealershipIds.Contains(item.Listing.DealershipId.Value)))
            .OrderByDescending(item => item.LastMessageAt)
            .Select(item => new
            {
                item.Id,
                item.ListingId,
                item.Listing.Title,
                item.BuyerId,
                BuyerName = item.Buyer.DisplayName,
                SellerName = item.Listing.Seller.DisplayName,
                DealerName = item.Listing.Dealership != null ? item.Listing.Dealership.Name : null,
                Photo = item.Listing.Car.Photos
                    .Where(photo => photo.IsPrimary)
                    .Select(photo => photo.Path)
                    .FirstOrDefault(),
                item.LastMessageAt,
                LastText = item.Messages
                    .OrderByDescending(message => message.CreatedAt)
                    .Select(message => message.Text)
                    .FirstOrDefault(),

                // Непрочитані — це чужі повідомлення без часу прочитання.
                Unread = item.Messages.Count(message =>
                    message.SenderId != userId && message.ReadAt == null),
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. conversations.Select(item => new ConversationSummary(
                item.Id,
                item.ListingId,
                item.Title,
                item.Photo,

                // Для покупця співрозмовник — салон або продавець; для того
                // боку — покупець.
                item.BuyerId == userId
                    ? item.DealerName ?? item.SellerName
                    : item.BuyerName,
                item.LastText,
                item.LastMessageAt,
                item.Unread)),
        ];
    }

    public async Task<ConversationDetails> StartAsync(
        long listingId,
        long buyerId,
        CancellationToken cancellationToken = default)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken)
            ?? throw new ListingNotFoundException(listingId);

        if (!Reachable.Contains(listing.Status))
        {
            throw new ListingNotFoundException(listingId);
        }

        // Писати самому собі немає сенсу — як і менеджерові салону в лот
        // власного салону.
        if (await access.CanManageAsync(listing, buyerId, cancellationToken))
        {
            throw new ChatNotAllowedException("Це ваше оголошення — писати нема кому.");
        }

        var existing = await dbContext.Conversations
            .FirstOrDefaultAsync(
                item => item.ListingId == listingId && item.BuyerId == buyerId,
                cancellationToken);

        if (existing is null)
        {
            var now = clock.UtcNow;

            existing = new Conversation
            {
                ListingId = listingId,
                BuyerId = buyerId,
                CreatedAt = now,
                LastMessageAt = now,
            };

            dbContext.Conversations.Add(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(existing.Id, buyerId, cancellationToken);
    }

    public async Task<ConversationDetails> GetAsync(
        long conversationId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await LoadForParticipantAsync(conversationId, userId, cancellationToken);

        // Відкрити розмову й означає прочитати її. Позначаємо одним запитом
        // прямо в базі, не вантажачи повідомлення в пам'ять заради поля.
        await dbContext.Messages
            .Where(message =>
                message.ConversationId == conversationId
                && message.SenderId != userId
                && message.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(message => message.ReadAt, clock.UtcNow),
                cancellationToken);

        var messages = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new MessageRecord(
                message.Id,
                message.ConversationId,
                message.SenderId,
                message.Sender.DisplayName,
                message.Text,
                message.CreatedAt,
                message.ReadAt != null))
            .ToListAsync(cancellationToken);

        var viewerIsSeller = conversation.BuyerId != userId;

        return new ConversationDetails(
            conversation.Id,
            conversation.ListingId,
            conversation.Listing.Title,
            await PrimaryPhotoAsync(conversation.ListingId, cancellationToken),
            viewerIsSeller
                ? conversation.Buyer.DisplayName
                : conversation.Listing.Dealership?.Name ?? conversation.Listing.Seller.DisplayName,
            viewerIsSeller,
            messages);
    }

    public async Task<MessageRecord> SendAsync(
        long conversationId,
        long senderId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var conversation = await LoadForParticipantAsync(conversationId, senderId, cancellationToken);

        var now = clock.UtcNow;

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Text = text.Trim(),
            CreatedAt = now,
        };

        dbContext.Messages.Add(message);

        // Час останнього повідомлення тримаємо в розмові, щоб список не
        // рахував його підзапитом на кожен рядок.
        conversation.LastMessageAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        var senderName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == senderId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var record = new MessageRecord(
            message.Id,
            conversationId,
            senderId,
            senderName,
            message.Text,
            message.CreatedAt,
            IsRead: false);

        await NotifyAsync(conversation, senderId, record, cancellationToken);

        return record;
    }

    public async Task<int> GetUnreadCountAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        var dealershipIds = await access.DealershipIdsOfAsync(userId, cancellationToken);

        return await dbContext.Messages
            .AsNoTracking()
            .CountAsync(
                message => message.SenderId != userId
                    && message.ReadAt == null
                    && (message.Conversation.BuyerId == userId
                        || message.Conversation.Listing.SellerId == userId
                        || (message.Conversation.Listing.DealershipId != null
                            && dealershipIds.Contains(message.Conversation.Listing.DealershipId.Value))),
                cancellationToken);
    }

    /// <summary>
    /// Повідомляє співрозмовника. Збій розсилки не має скасовувати
    /// повідомлення — воно вже збережене.
    /// </summary>
    private async Task NotifyAsync(
        Conversation conversation,
        long senderId,
        MessageRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            // Кому саме — залежить від того, хто написав. Якщо писав покупець,
            // читатиме бік продавця; якщо продавець — покупець.
            var recipients = senderId == conversation.BuyerId
                ? await SellerSideAsync(conversation.Listing, cancellationToken)
                : [conversation.BuyerId];

            await notifier.MessageSentAsync(recipients, record, cancellationToken);
        }
        catch (Exception)
        {
            // Мовчки: канал — зручність, а не умова доставки. Повідомлення
            // з'явиться при наступному відкритті розмови.
        }
    }

    /// <summary>
    /// Хто відповідає з боку продавця. Для салонного лота це весь персонал:
    /// відповісти має змогти будь-хто, а не лише той, хто подав оголошення.
    /// </summary>
    private async Task<IReadOnlyList<long>> SellerSideAsync(
        Listing listing,
        CancellationToken cancellationToken)
    {
        if (listing.DealershipId is not { } dealershipId)
        {
            return [listing.SellerId];
        }

        return await dbContext.DealershipMembers
            .AsNoTracking()
            .Where(member => member.DealershipId == dealershipId)
            .Select(member => member.UserId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Завантажує розмову й одразу перевіряє, що той, хто питає, — її
    /// учасник. Стороннім віддаємо «не знайдено», а не «немає доступу»:
    /// існування чужого листування не їхня справа.
    /// </summary>
    private async Task<Conversation> LoadForParticipantAsync(
        long conversationId,
        long userId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .Include(item => item.Buyer)
            .Include(item => item.Listing).ThenInclude(listing => listing.Seller)
            .Include(item => item.Listing).ThenInclude(listing => listing.Dealership)
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw new ConversationNotFoundException(conversationId);

        var isParticipant = conversation.BuyerId == userId
            || await access.CanManageAsync(conversation.Listing, userId, cancellationToken);

        if (!isParticipant)
        {
            throw new ConversationNotFoundException(conversationId);
        }

        return conversation;
    }

    private async Task<string?> PrimaryPhotoAsync(long listingId, CancellationToken cancellationToken)
    {
        return await dbContext.CarPhotos
            .AsNoTracking()
            .Where(photo => photo.Car.ListingId == listingId && photo.IsPrimary)
            .Select(photo => photo.Path)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
