using CargaMasiva.Domain;

namespace CargaMasiva.UnitTests;

public class LimpiadorFilaTests
{
    [Fact]
    public void Fila_vacia_se_omite()
    {
        var r = LimpiadorFila.Limpiar(2, null, "  ", null, "");
        Assert.Equal(TipoFila.Omitida, r.Tipo);
    }

    [Fact]
    public void Nombre_y_precio_vacios_usan_valor_por_defecto()
    {
        var r = LimpiadorFila.Limpiar(2, "2025-04", " p0001 ", null, null);
        Assert.Equal(TipoFila.Valida, r.Tipo);
        Assert.Equal("P0001", r.Valida!.Codigo);
        Assert.Equal(LimpiadorFila.NombrePorDefecto, r.Valida.Nombre);
        Assert.Equal(0m, r.Valida.Precio);
    }

    [Fact]
    public void Periodo_que_Excel_convirtio_en_fecha_se_acepta()
    {
        var r = LimpiadorFila.Limpiar(2, new DateTime(2025, 3, 1), "P1", "x", 10.5);
        Assert.Equal("2025-03", r.Valida!.Periodo);
    }

    [Theory]
    [InlineData("marzo")]
    [InlineData("2025-13")]
    [InlineData(null)]
    public void Periodo_invalido_marca_la_fila(string? periodo)
    {
        var r = LimpiadorFila.Limpiar(2, periodo, "P1", "x", 1);
        Assert.Equal(TipoFila.Invalida, r.Tipo);
    }

    [Fact]
    public void Codigo_vacio_marca_la_fila()
    {
        var r = LimpiadorFila.Limpiar(5, "2025-01", null, "x", 1);
        Assert.Equal(TipoFila.Invalida, r.Tipo);
        Assert.Equal(5, r.Invalida!.Fila);
    }

    [Theory]
    [InlineData("12.50", 12.50)]
    [InlineData("12,50", 12.50)]
    public void Precio_como_texto_se_convierte(string precio, double esperado)
    {
        var r = LimpiadorFila.Limpiar(2, "2025-01", "P1", "x", precio);
        Assert.Equal((decimal)esperado, r.Valida!.Precio);
    }

    [Fact]
    public void Precio_no_numerico_o_negativo_marca_la_fila()
    {
        Assert.Equal(TipoFila.Invalida, LimpiadorFila.Limpiar(2, "2025-01", "P1", "x", "abc").Tipo);
        Assert.Equal(TipoFila.Invalida, LimpiadorFila.Limpiar(2, "2025-01", "P1", "x", -1).Tipo);
    }

    [Fact]
    public void Resultado_con_filas_invalidas_no_es_exitoso()
    {
        Assert.Equal(Resultados.ConObservaciones, Resultados.Decidir(Resultados.Exitoso, 10, 1));
        Assert.Equal(Resultados.Rechazado, Resultados.Decidir(Resultados.Exitoso, 0, 3));
        Assert.Equal(Resultados.Exitoso, Resultados.Decidir(Resultados.Exitoso, 10, 0));
    }
}