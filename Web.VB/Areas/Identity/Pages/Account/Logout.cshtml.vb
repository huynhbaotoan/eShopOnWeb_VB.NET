Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Authorization
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Mvc.RazorPages
Imports Microsoft.eShopWeb.Infrastructure.Identity
Imports Microsoft.Extensions.Logging

Namespace Areas.Identity.Pages.Account
    <AllowAnonymous>
    Public Class LogoutModel
        Inherits PageModel

        Private ReadOnly _signInManager As SignInManager(Of ApplicationUser)
        Private ReadOnly _logger As ILogger(Of LogoutModel)

        Public Sub New(ByVal signInManager As SignInManager(Of ApplicationUser), ByVal logger As ILogger(Of LogoutModel))
            _signInManager = signInManager
            _logger = logger
        End Sub

        Public Async Function OnGet(ByVal Optional returnUrl As String = Nothing) As Task(Of IActionResult)
            Await _signInManager.SignOutAsync()
            _logger.LogInformation("User logged out.")

            If returnUrl IsNot Nothing Then
                Return LocalRedirect(returnUrl)
            End If

            Return Page()
        End Function

        Public Async Function OnPost(ByVal Optional returnUrl As String = Nothing) As Task(Of IActionResult)
            Await _signInManager.SignOutAsync()
            _logger.LogInformation("User logged out.")

            If returnUrl IsNot Nothing Then
                Return LocalRedirect(returnUrl)
            End If

            Return RedirectToPage("/Index")
        End Function
    End Class
End Namespace
