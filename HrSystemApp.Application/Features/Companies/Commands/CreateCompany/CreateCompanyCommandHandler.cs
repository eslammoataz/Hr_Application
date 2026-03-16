using AutoMapper;
using MediatR;
using HrSystemApp.Application.Common;
using HrSystemApp.Application.DTOs.Companies;
using HrSystemApp.Application.Interfaces;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Application.Features.Companies.Commands.CreateCompany;

public class CreateCompanyCommandHandler : IRequestHandler<CreateCompanyCommand, Result<CompanyResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateCompanyCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<CompanyResponse>> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = _mapper.Map<Company>(request);

        await _unitOfWork.Companies.AddAsync(company, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(_mapper.Map<CompanyResponse>(company));
    }
}
