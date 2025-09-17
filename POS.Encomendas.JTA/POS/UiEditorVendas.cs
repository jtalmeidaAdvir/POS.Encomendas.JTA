using Primavera.Extensibility.POS.Editors;
using Primavera.Extensibility.BusinessEntities.ExtensibilityService.EventArgs;
using StdBE100;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;


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
                int i = 0;
                foreach (string idEncomenda in form.EncomendasSelecionadas)
                {
                    if (i == 0)
                    {
                        double descontoEntidade = form.GetDescontoEntidade(idEncomenda);
                        form.CarregarDadosDocumentoVenda(this.DocumentoVenda);
                        this.DocumentoVenda.DescEntidade = descontoEntidade;

                        i++;

                    }
        
                }
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
                            double precUnit = linhasdoCabecDoc.DaValor<double>("PrecUnit");
                            string iva = linhasdoCabecDoc.DaValor<string>("CodIva");
                            float taxaIva = linhasdoCabecDoc.DaValor<float>("TaxaIva");
                            double desconto = linhasdoCabecDoc.DaValor<double>("Desconto1");
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
                            novaLinha.PrecUnit = precUnit;
                            novaLinha.CodIva = iva;
                            novaLinha.TaxaIva = taxaIva;
                            novaLinha.Desconto1 = desconto;
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

                      //  MessageBox.Show(linha.Quantidade.ToString().Replace(",", "."));
                        string updateQuantTrans = $@"
                    UPDATE LinhasDocStatus
                    SET QuantTrans = {linha.Quantidade} /2
                    WHERE IdLinhasDoc = '{idLinhaEncomenda}'";
                        //ISNULL(QuantTrans, 0) + 
                        BSO.DSO.ExecuteSQL(updateQuantTrans);



                        //INSERIR OU MARTELAR LinhasDocTrans
                        //BUSCAR OS IDs DAS LINHAS DA FATURA
                       // this.DocumentoVenda.Linhas.NumItens

                        string insertLinhasDocTrans = $@"
                        INSERT INTO LinhasDocTrans (IdLinhasDoc,IdLinhasDocOrigem,QuantTrans)
                        VALUES
                        ('{linha.IdLinha}','{idLinhaEncomenda}',{linha.Quantidade.ToString().Replace(",", ".")})";

                        BSO.DSO.ExecuteSQL(insertLinhasDocTrans);


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

        public override void AntesDeGravar(ref bool Cancel, ExtensibilityEventArgs e)
        {
            try
            {
                // Verificar reservas para cada encomenda selecionada
                foreach (string idEncomenda in EncomendasUsadas)
                {
                    // Buscar dados da encomenda para construir a descrição
                    string queryEncomenda = $@"
                        SELECT TipoDoc, Serie, NumDoc 
                        FROM CabecDoc 
                        WHERE Id = '{idEncomenda}'";

                    var dadosEncomenda = BSO.Consulta(queryEncomenda);
                    if (!dadosEncomenda.NoFim())
                    {
                        dadosEncomenda.Inicio();
                        string tipoDoc = dadosEncomenda.DaValor<string>("TipoDoc");
                        string serie = dadosEncomenda.DaValor<string>("Serie");
                        string numDoc = dadosEncomenda.DaValor<string>("NumDoc");

                        // Construir a descrição do destino para verificar reservas
                        string descricaoDestino = $"NE {tipoDoc}.{serie}/{numDoc}";

                        // Verificar se existem reservas para esta encomenda
                        string queryReservas = $@"
                            SELECT * FROM INV_Reservas 
                            WHERE DescricaoDestino = '{descricaoDestino}'";

                        var reservas = BSO.Consulta(queryReservas);
                        reservas.Inicio();

                        while (!reservas.NoFim())
                        {
                            // Pega o ID da reserva
                            string idReserva = reservas.DaValor<string>("Id");

                            // Anula a reserva
                            BSO.Inventario.Reservas.AnularReserva(idReserva);

                            // Avança para o próximo registo
                            reservas.Seguinte();
                        }

              
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao verificar reservas: " + ex.Message);
                Cancel = true;
            }
        }
    }
}
