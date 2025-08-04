using Primavera.Extensibility.POS.Editors;
using Primavera.Extensibility.BusinessEntities.ExtensibilityService.EventArgs;
using StdBE100;
using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace POS.Encomendas.JTA.POS
{
    public class UiEditorVendas : EditorVendas
    {
        public string ClientePOS { get; set; }

        private static HashSet<string> EncomendasUsadas = new HashSet<string>();

        // Mapeamento linha venda => linha encomenda original
        private Dictionary<string, string> linhasCriadasParaEncomendas = new Dictionary<string, string>();

        public override void TeclaPressionada(int KeyCode, int Shift, ExtensibilityEventArgs e)
        {
            ClientePOS = this.DocumentoVenda.Entidade;
            if (string.IsNullOrEmpty(ClientePOS))
            {
                ClientePOS = "VD";
            }

            EncomendasUsadas.Clear();
            linhasCriadasParaEncomendas.Clear();

            try
            {
                this.DocumentoVenda.Linhas.RemoveTodos();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao limpar linhas do documento: " + ex.Message);
            }

            EditorEscolherEncomendas form = new EditorEscolherEncomendas(BSO, PSO, ClientePOS, EncomendasUsadas);

            if (form.ShowDialog() == DialogResult.OK)
            {
                List<string> idsSelecionados = form.EncomendasSelecionadas;

                try
                {
                    foreach (string id in idsSelecionados)
                    {
                        var linhasdoCabecDoc = GetLinhasComQuantTrans(id);
                        linhasdoCabecDoc.Inicio();

                        while (!linhasdoCabecDoc.NoFim())
                        {
                            string artigo = linhasdoCabecDoc.DaValor<string>("Artigo");
                            string lote = linhasdoCabecDoc.DaValor<string>("Lote");
                            double quantidadeOriginal = linhasdoCabecDoc.DaValor<double>("Quantidade");
                            double quantTrans = linhasdoCabecDoc.DaValor<double>("QuantTrans"); // Quantidade já transformada
                            string idLinhaOriginal = linhasdoCabecDoc.DaValor<string>("Id");

                            double quantidadeDisponivel = quantidadeOriginal - quantTrans;

                            // Se já não existe quantidade disponível, passa à próxima linha
                            if (quantidadeDisponivel <= 0 || string.IsNullOrEmpty(artigo))
                            {
                                linhasdoCabecDoc.Seguinte();
                                continue;
                            }

                            var documentoV = BSO.Vendas.Documentos.AdicionaLinha(this.DocumentoVenda, artigo);

                            var novaLinha = documentoV.Linhas.GetEdita(documentoV.Linhas.NumItens);
                            novaLinha.Quantidade = quantidadeDisponivel;
                            novaLinha.EstadoOrigem = "DISP";
                            novaLinha.Lote = lote;

                            // Mapeia o ID da nova linha para a linha original da encomenda
                            linhasCriadasParaEncomendas[novaLinha.IdLinha] = idLinhaOriginal;

                            linhasdoCabecDoc.Seguinte();
                        }

                        EncomendasUsadas.Add(id);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao inserir linha: " + ex.Message);
                }
            }
        }

        // Método que retorna as linhas do documento juntando as quantidades transformadas (QuantTrans)
        private StdBELista GetLinhasComQuantTrans(string id)
        {
            var querysql = $@"
                SELECT LD.*, ISNULL(LDS.QuantTrans, 0) AS QuantTrans
                FROM LinhasDoc LD
                LEFT JOIN LinhasDocStatus LDS ON LDS.IdLinhasDoc = LD.Id
                WHERE LD.IdCabecDoc = '{id}'
                ORDER BY LD.NumLinha ASC";
            return BSO.Consulta(querysql);
        }

        public override void DepoisDeGravar(string Filial, string Serie, string Tipo, int NumDoc, ExtensibilityEventArgs e)
        {
            try
            {
                int numLinhas = DocumentoVenda.Linhas.NumItens;

                for (int i = 1; i <= numLinhas; i++)
                {
                    var linha = DocumentoVenda.Linhas.GetEdita(i);

                    if (linha == null || string.IsNullOrEmpty(linha.Artigo))
                        continue;

                    if (linhasCriadasParaEncomendas.TryGetValue(linha.IdLinha, out string idLinhaEncomenda))
                    {
                        string updateQuantTrans = $@"
                    UPDATE LinhasDocStatus
                    SET QuantTrans = ISNULL(QuantTrans, 0) + {linha.Quantidade.ToString().Replace(",", ".")}
                    WHERE IdLinhasDoc = '{idLinhaEncomenda}'";

                        BSO.DSO.ExecuteSQL(updateQuantTrans);
                    }
                }

                foreach (string idEncomenda in EncomendasUsadas)
                {
                    string verificaPendentes = $@"
                SELECT COUNT(*) AS Pendentes
                FROM LinhasDocStatus LDS
                INNER JOIN LinhasDoc LD ON LDS.IdLinhasDoc = LD.Id
                WHERE LD.IdCabecDoc = '{idEncomenda}' AND ISNULL(LDS.QuantTrans, 0) < LD.Quantidade";

                    var resultado = BSO.Consulta(verificaPendentes);
                    resultado.Inicio();
                    int pendentes = resultado.DaValor<int>("Pendentes");

                    if (pendentes == 0)
                    {
                        string fecharEncomenda = $@"
                    UPDATE CabecDocStatus 
                    SET Fechado = 1 
                    WHERE IdCabecDoc = '{idEncomenda}'";

                        BSO.DSO.ExecuteSQL(fecharEncomenda);
                    }
                }

                EncomendasUsadas.Clear();
                linhasCriadasParaEncomendas.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao atualizar estado das encomendas: " + ex.Message);
            }
        }

        public override void ClienteIdentificado(string Cliente, ref bool Cancel, ExtensibilityEventArgs e)
        {
            ClientePOS = Cliente.Trim();
        }

        public override void DepoisDeEditar(ExtensibilityEventArgs e)
        {
            ClientePOS = this.DocumentoVenda.Entidade.Trim();
            EncomendasUsadas.Clear();
            linhasCriadasParaEncomendas.Clear();
        }
    }
}
