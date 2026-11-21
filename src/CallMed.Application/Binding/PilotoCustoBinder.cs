using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace MKSANCrud.Application.Binding;

// Campos HTML number usam ponto, mesmo com a aplicação em pt-BR.
public sealed class PilotoCustoBinder:IModelBinder {
 public static bool Tentar(string text,out decimal? valor) {
  valor=null;text=text.Trim();if(text.Length==0)return true;
  if(!Regex.IsMatch(text,@"^\d{1,5}([.,]\d{1,2})?$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))return false;
  if(!decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)||n>10000)return false;
  valor=n;return true;
 }
 public Task BindModelAsync(ModelBindingContext context) {
  var value=context.ValueProvider.GetValue(context.ModelName);
  if(value!=ValueProviderResult.None)context.ModelState.SetModelValue(context.ModelName,value);
  if(value.Length<=1&&Tentar(value.FirstValue??"",out var n))context.Result=ModelBindingResult.Success(n);
  else context.ModelState.TryAddModelError(context.ModelName,"Informe o custo por hora em reais, com até duas casas decimais e sem separador de milhar.");
  return Task.CompletedTask;
 }
}
