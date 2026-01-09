using KamiYomu.Web.Entities;
using KamiYomu.Web.Entities.Kavita;

namespace KamiYomu.Web.Infrastructure.Services.Interfaces;

public interface IKavitaService
{

    Task<IReadOnlyList<KavitaLibrary>> LoadAllCollectionsAsync(CancellationToken cancellationToken);
    Task<bool> TryConnectToKavita(KavitaSettings kavitaSettings, CancellationToken cancellationToken);
    Task UpdateAllCollectionsAsync(CancellationToken cancellationToken);
}
