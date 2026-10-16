using Boilerate.Application.Common.Interfaces;

namespace Boilerate.Application.Common.Mailing;

public interface IEmailTemplateService : ITransientService
{
    string GenerateEmailTemplate<T>(string templateName, T mailTemplateModel);
}
