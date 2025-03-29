using Application.Interface;
using Application.Interfaces;
using Application.Reports.DTOs;
using Application.Reports.Interface;
using Domain.Login.Interfaces;
using Domain.Relatorios;
using Domain.Relatorios.AcumuladoMensal;

namespace Application.Reports.Service
{
    public class AcumuladoMensalReportService : IAcumuladoMensalReportService
    {
        private readonly IRendimentoService _rendimentoService;
        private readonly IDespesaService _despesaService;
        private readonly IInvestimentoService _investimentoService;

        private readonly IAcumuladoMensalReportRepository _acumuladoMensalReportRepository;
        private readonly IUsuarioLogado _usuarioLogado;

        public AcumuladoMensalReportService(
            IRendimentoService rendimento,
            IAcumuladoMensalReportRepository acumuladoMensalReport,
            IUsuarioLogado usuarioLogado,
            IDespesaService despesaService,
            IInvestimentoService investimentoService)
        {
            _rendimentoService = rendimento;
            _acumuladoMensalReportRepository = acumuladoMensalReport;
            _usuarioLogado = usuarioLogado;
            _despesaService = despesaService;
            _investimentoService = investimentoService;
        }

        public async Task<AcumuladoMensalReportDTO> ObterReport(int mes, int ano, TipoTransacao? tipoTransacao)
        {
            var report = await _acumuladoMensalReportRepository.Obter(mes, ano, _usuarioLogado.Id);

            AcumuladoMensalReportDTO reportDTO = report;

            if (tipoTransacao.HasValue)
            {
                switch (tipoTransacao)
                {
                    case TipoTransacao.Rendimento:
                        reportDTO.Rendimentos = await _rendimentoService.ObterRendimentoMes(mes, ano);
                        break;
                    case TipoTransacao.Despesa:
                        reportDTO.Despesas = await _despesaService.ObterMesAno(mes, ano);
                        break;
                    case TipoTransacao.Investimento:
                        reportDTO.Investimentos = await _investimentoService.ObterMesAno(mes, ano);
                        break;
                }
            }

            return reportDTO;
        }
    }
}
